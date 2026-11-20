using System.Net;
using Google;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Todo.Application.Common;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Truncated exponential backoff for Sheets calls. Kept separate from the executor so the delay
/// schedule is a pure function that can be asserted on without waiting.
/// </summary>
public sealed class RetryPolicy
{
    public const int DefaultMaxAttempts = 5;

    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(1);

    private const double MaxJitterFraction = 0.25;

    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(8);

    public RetryPolicy(int maxAttempts = DefaultMaxAttempts, TimeSpan? baseDelay = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        MaxAttempts = maxAttempts;
        BaseDelay = baseDelay ?? DefaultBaseDelay;
    }

    public int MaxAttempts { get; }

    public TimeSpan BaseDelay { get; }

    /// <summary>
    /// Delay to wait before attempt <paramref name="attempt"/> + 1. <paramref name="jitterSample"/>
    /// is in [0, 1) and only ever stretches the delay by <see cref="MaxJitterFraction"/>, so
    /// concurrent callers do not retry in lockstep without ever exceeding the stated budget.
    /// </summary>
    public TimeSpan ComputeDelay(int attempt, double jitterSample)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var exponential = BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var capped = Math.Min(exponential, MaxDelay.TotalMilliseconds);
        var jittered = capped * (1 + (Math.Clamp(jitterSample, 0, 1) * MaxJitterFraction));

        return TimeSpan.FromMilliseconds(jittered);
    }

    public TimeSpan DelayFor(int attempt) => ComputeDelay(attempt, Random.Shared.NextDouble());

    public static bool IsRetryableStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}

/// <summary>
/// Turns every SDK exception into a <see cref="TodoError"/> after exhausting the retry budget.
/// The caller's token is never swallowed: cancellation surfaces as
/// <see cref="OperationCanceledException"/> because the CLI must be interruptible.
/// </summary>
public sealed class GoogleRequestExecutor : IRequestExecutor
{
    private readonly RetryPolicy _policy;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly ILogger<GoogleRequestExecutor> _logger;

    public GoogleRequestExecutor(
        ILogger<GoogleRequestExecutor>? logger = null,
        RetryPolicy? policy = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _logger = logger ?? NullLogger<GoogleRequestExecutor>.Instance;
        _policy = policy ?? new RetryPolicy();
        _delay = delay ?? Task.Delay;
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var value = await operation(cancellationToken).ConfigureAwait(false);
                return Result<T>.Success(value);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var (error, retryable) = Classify(exception);

                if (!retryable || attempt >= _policy.MaxAttempts)
                {
                    SheetsLog.RequestFailed(_logger, exception, attempt, _policy.MaxAttempts);

                    return Result<T>.Failure(error);
                }

                var delay = _policy.DelayFor(attempt);

                SheetsLog.RequestRetrying(_logger, attempt, _policy.MaxAttempts, delay);

                await _delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static (TodoError Error, bool Retryable) Classify(Exception exception) => exception switch
    {
        GoogleApiException google => ClassifyGoogleApiError(google),
        HttpRequestException http => (
            TodoError.Unavailable($"Could not reach Google Sheets: {http.Message}"),
            true),
        TaskCanceledException => (
            TodoError.Unavailable("The Google Sheets request timed out. Try again shortly."),
            true),
        _ => (
            TodoError.Unexpected($"Google Sheets returned an unexpected response: {exception.Message}"),
            false),
    };

    private static (TodoError Error, bool Retryable) ClassifyGoogleApiError(GoogleApiException exception)
    {
        var status = exception.HttpStatusCode;

        if (RetryPolicy.IsRetryableStatus(status))
        {
            return status == HttpStatusCode.TooManyRequests
                ? (TodoError.Unavailable("Google Sheets quota exceeded. Try again shortly."), true)
                : (TodoError.Unavailable("Google Sheets is temporarily unavailable. Try again shortly."), true);
        }

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => (
                TodoError.Permission(
                    "Google Sheets rejected the request. Share the spreadsheet with the service account as Editor."),
                false),
            HttpStatusCode.NotFound => (
                TodoError.NotFound("The spreadsheet or sheet was not found. Check the spreadsheet ID and sharing settings."),
                false),
            HttpStatusCode.BadRequest => (
                TodoError.Conflict($"Google Sheets rejected the request: {Describe(exception)}"),
                false),
            _ => (TodoError.Unexpected($"Google Sheets returned HTTP {(int)status}: {Describe(exception)}"), false),
        };
    }

    private static string Describe(GoogleApiException exception) =>
        string.IsNullOrWhiteSpace(exception.Error?.Message) ? exception.Message : exception.Error!.Message;
}
