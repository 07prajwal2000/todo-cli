using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Obtains the spreadsheet id and the service-account credential from the application's stores and
/// builds one authorized <see cref="SheetsService"/> per process. The credential JSON is parsed here
/// rather than through the configuration adapter so this project stays the only one that knows
/// about Google's auth types while still being usable on its own.
/// </summary>
public sealed class SheetsConnectionFactory : IDisposable
{
    public const string ApplicationName = "todo-cli";

    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ISettingsStore _settingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<SheetsConnectionFactory> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private SheetsService? _service;
    private string? _spreadsheetId;
    private ServiceAccountIdentity? _identity;

    public SheetsConnectionFactory(
        ISettingsStore settingsStore,
        ICredentialStore credentialStore,
        ILogger<SheetsConnectionFactory>? logger = null)
    {
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _logger = logger ?? NullLogger<SheetsConnectionFactory>.Instance;
    }

    /// <summary>The spreadsheet id once it has been resolved, otherwise null.</summary>
    public string? SpreadsheetId => _spreadsheetId;

    public async Task<Result<SheetsService>> GetAsync(CancellationToken cancellationToken)
    {
        if (_service is { } cached)
        {
            return Result<SheetsService>.Success(cached);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_service is { } built)
            {
                return Result<SheetsService>.Success(built);
            }

            var spreadsheetId = await LoadSpreadsheetIdAsync(cancellationToken).ConfigureAwait(false);
            if (spreadsheetId.IsFailure)
            {
                return Result<SheetsService>.Failure(spreadsheetId.Error!);
            }

            var identity = await LoadIdentityAsync(cancellationToken).ConfigureAwait(false);
            if (identity.IsFailure)
            {
                return Result<SheetsService>.Failure(identity.Error!);
            }

            _service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = identity.Value.Credential,
                ApplicationName = ApplicationName,
            });

            SheetsLog.ClientCreated(_logger, identity.Value.Email);

            return Result<SheetsService>.Success(_service);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result<string>> GetSpreadsheetIdAsync(CancellationToken cancellationToken)
    {
        if (_spreadsheetId is { } cached)
        {
            return Result<string>.Success(cached);
        }

        return await LoadSpreadsheetIdAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<string?>> GetServiceAccountEmailAsync(CancellationToken cancellationToken)
    {
        if (_identity is { } cached)
        {
            return Result<string?>.Success(cached.Email);
        }

        var identity = await LoadIdentityAsync(cancellationToken).ConfigureAwait(false);

        return identity.IsSuccess
            ? Result<string?>.Success(identity.Value.Email)
            : Result<string?>.Failure(identity.Error!);
    }

    public void Dispose() => _gate.Dispose();

    private async Task<Result<string>> LoadSpreadsheetIdAsync(CancellationToken cancellationToken)
    {
        if (_spreadsheetId is { } cached)
        {
            return Result<string>.Success(cached);
        }

        var settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.TryGetValue(out var loaded))
        {
            return Result<string>.Failure(settings.Error!);
        }

        if (string.IsNullOrWhiteSpace(loaded.SpreadsheetId))
        {
            return Result<string>.Failure(TodoError.Configuration(
                "No spreadsheet is configured. Run 'todo init' to set the spreadsheet ID."));
        }

        _spreadsheetId = loaded.SpreadsheetId.Trim();
        return Result<string>.Success(_spreadsheetId);
    }

    private async Task<Result<ServiceAccountIdentity>> LoadIdentityAsync(CancellationToken cancellationToken)
    {
        if (_identity is { } cached)
        {
            return Result<ServiceAccountIdentity>.Success(cached);
        }

        var raw = await _credentialStore.ReadRawAsync(cancellationToken).ConfigureAwait(false);
        if (!raw.TryGetValue(out var json))
        {
            // Whatever the store reported (usually "file missing"), from the caller's point of view
            // the account is simply not configured yet — the remedy is always 'todo init'.
            return Result<ServiceAccountIdentity>.Failure(TodoError.Configuration(
                $"The credentials at '{_credentialStore.DescribeLocation()}' could not be read: {raw.Error!.Message} " +
                "Run 'todo init' to write a service-account key."));
        }

        var parsed = ParseServiceAccount(json);

        if (!parsed.IsSuccess)
        {
            return Result<ServiceAccountIdentity>.Failure(parsed.Error!);
        }

        _identity = parsed.Value;
        return parsed;
    }

    private Result<ServiceAccountIdentity> ParseServiceAccount(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<ServiceAccountIdentity>.Failure(TodoError.Configuration(
                $"The credentials at '{_credentialStore.DescribeLocation()}' are empty. " +
                "Run 'todo init' to write a service-account key."));
        }

        ServiceAccountKey? key;

        try
        {
            key = JsonSerializer.Deserialize<ServiceAccountKey>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            return Result<ServiceAccountIdentity>.Failure(TodoError.Configuration(
                $"The credentials at '{_credentialStore.DescribeLocation()}' are not valid JSON: {exception.Message}"));
        }

        if (key is null || string.IsNullOrWhiteSpace(key.ClientEmail) || string.IsNullOrWhiteSpace(key.PrivateKey))
        {
            return Result<ServiceAccountIdentity>.Failure(TodoError.Configuration(
                "The service-account key is missing 'client_email' or 'private_key'. " +
                "Download a new key from Google Cloud and run 'todo init' again."));
        }

        try
        {
            var credential = new ServiceAccountCredential(
                new ServiceAccountCredential.Initializer(key.ClientEmail)
                {
                    Scopes = Scopes,
                }.FromPrivateKey(key.PrivateKey));

            return Result<ServiceAccountIdentity>.Success(new ServiceAccountIdentity(key.ClientEmail, credential));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Every path here means the user-supplied key cannot be turned into a credential, and
            // the SDK's exception types for that (RSA import, PEM parsing) are not a stable contract.
            return Result<ServiceAccountIdentity>.Failure(TodoError.Configuration(
                $"The service-account private key could not be read: {exception.Message}"));
        }
    }

    private sealed record ServiceAccountIdentity(string? Email, ServiceAccountCredential Credential);

    private sealed record ServiceAccountKey
    {
        [JsonPropertyName("client_email")]
        public string? ClientEmail { get; init; }

        [JsonPropertyName("private_key")]
        public string? PrivateKey { get; init; }
    }
}
