using System.Net;
using Google;
using Todo.Application.Common;

namespace Todo.Infrastructure.GoogleSheets.Tests;

public sealed class GoogleRequestExecutorTests
{
    private static GoogleApiException ApiError(HttpStatusCode statusCode, string message = "failed") =>
        new("sheets", message) { HttpStatusCode = statusCode };

    private sealed class DelayRecorder
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private static GoogleRequestExecutor CreateExecutor(DelayRecorder recorder, int maxAttempts = 5) =>
        new(
            logger: null,
            policy: new RetryPolicy(maxAttempts, TimeSpan.FromSeconds(1)),
            delay: recorder.DelayAsync);

    [Fact]
    public async Task ExecuteAsync_returns_the_value_on_the_first_success()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);

        var result = await executor.ExecuteAsync(_ => Task.FromResult(42), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Empty(recorder.Delays);
    }

    [Fact]
    public async Task ExecuteAsync_retries_a_429_and_then_succeeds()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);
        var attempts = 0;

        var result = await executor.ExecuteAsync(
            _ =>
            {
                attempts++;
                return attempts < 3
                    ? Task.FromException<string>(ApiError(HttpStatusCode.TooManyRequests))
                    : Task.FromResult("ok");
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
        Assert.Equal(3, attempts);
        Assert.Equal(2, recorder.Delays.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task ExecuteAsync_retries_transient_server_failures(HttpStatusCode statusCode)
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);
        var attempts = 0;

        var result = await executor.ExecuteAsync(
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? Task.FromException<int>(ApiError(statusCode))
                    : Task.FromResult(1);
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_stops_after_max_attempts_and_maps_429_to_unavailable()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder, maxAttempts: 5);
        var attempts = 0;

        var result = await executor.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(ApiError(HttpStatusCode.TooManyRequests));
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
        Assert.Equal("Google Sheets quota exceeded. Try again shortly.", result.Error!.Message);
        Assert.Equal(5, attempts);
        Assert.Equal(4, recorder.Delays.Count);
    }

    [Fact]
    public async Task ExecuteAsync_uses_truncated_exponential_backoff()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder, maxAttempts: 5);

        await executor.ExecuteAsync<int>(
            _ => Task.FromException<int>(ApiError(HttpStatusCode.TooManyRequests)),
            CancellationToken.None);

        Assert.Equal(4, recorder.Delays.Count);

        for (var index = 0; index < recorder.Delays.Count; index++)
        {
            var nominal = TimeSpan.FromSeconds(Math.Pow(2, index));
            var ceiling = TimeSpan.FromTicks((long)(nominal.Ticks * 1.25));

            Assert.InRange(recorder.Delays[index], nominal, ceiling);
        }
    }

    [Fact]
    public async Task ExecuteAsync_maps_403_to_permission_without_retrying()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);
        var attempts = 0;

        var result = await executor.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(ApiError(HttpStatusCode.Forbidden));
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Permission, result.Error!.Code);
        Assert.Equal(1, attempts);
        Assert.Empty(recorder.Delays);
    }

    [Fact]
    public async Task ExecuteAsync_maps_401_to_permission()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);

        var result = await executor.ExecuteAsync<int>(
            _ => Task.FromException<int>(ApiError(HttpStatusCode.Unauthorized)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Permission, result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_maps_404_to_not_found_without_retrying()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);
        var attempts = 0;

        var result = await executor.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(ApiError(HttpStatusCode.NotFound));
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.NotFound, result.Error!.Code);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_retries_http_request_failures_then_maps_to_unavailable()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder, maxAttempts: 3);

        var result = await executor.ExecuteAsync<int>(
            _ => Task.FromException<int>(new HttpRequestException("dns")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
        Assert.Equal(2, recorder.Delays.Count);
    }

    [Fact]
    public async Task ExecuteAsync_retries_a_client_timeout_that_did_not_cancel_the_caller()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder, maxAttempts: 3);

        var result = await executor.ExecuteAsync<int>(
            _ => Task.FromException<int>(new TaskCanceledException("timed out")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
        Assert.Equal(2, recorder.Delays.Count);
    }

    [Fact]
    public async Task ExecuteAsync_maps_unexpected_exceptions_to_unexpected_without_retrying()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);
        var attempts = 0;

        var result = await executor.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(new InvalidOperationException("boom"));
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unexpected, result.Error!.Code);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_swallow_caller_cancellation()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync<int>(
                _ => Task.FromException<int>(ApiError(HttpStatusCode.TooManyRequests)),
                cancellation.Token));
    }

    [Fact]
    public async Task ExecuteAsync_propagates_cancellation_raised_mid_operation()
    {
        var recorder = new DelayRecorder();
        var executor = CreateExecutor(recorder);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await executor.ExecuteAsync<int>(
                _ =>
                {
                    cancellation.Cancel();
                    return Task.FromException<int>(ApiError(HttpStatusCode.ServiceUnavailable));
                },
                cancellation.Token);
        });
    }

    [Fact]
    public async Task ExecuteAsync_honours_cancellation_while_backing_off()
    {
        using var cancellation = new CancellationTokenSource();
        var executor = new GoogleRequestExecutor(
            logger: null,
            policy: new RetryPolicy(5, TimeSpan.FromSeconds(1)),
            delay: async (_, token) =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync<int>(
                _ => Task.FromException<int>(ApiError(HttpStatusCode.TooManyRequests)),
                cancellation.Token));
    }
}
