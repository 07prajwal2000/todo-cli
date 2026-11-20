using Todo.Application.Common;
using Todo.Application.Spaces;
using Todo.Domain.Spaces;

namespace Todo.Application.Setup;

public sealed record InitRequest
{
    public required string CredentialsJson { get; init; }

    public required string SpreadsheetId { get; init; }

    public required string DefaultSpace { get; init; }

    public string ServiceAccountEmail { get; init; } = string.Empty;
}

/// <summary>
/// Validates and persists setup, then bootstraps the sheet. Kept separate from the CLI's prompts
/// so the whole flow is testable without a terminal.
/// </summary>
public sealed class SetupService(
    ISettingsStore settingsStore,
    ICredentialStore credentialStore,
    IBackendProbe probe,
    SpaceService spaceService)
{
    public async Task<Result<TodoSettings>> InitializeAsync(InitRequest request, CancellationToken cancellationToken)
    {
        if (!SpaceName.IsValid(request.DefaultSpace, out var spaceError))
        {
            return Result<TodoSettings>.Failure(TodoError.Validation(spaceError!));
        }

        if (string.IsNullOrWhiteSpace(request.SpreadsheetId))
        {
            return Result<TodoSettings>.Failure(TodoError.Validation("A spreadsheet ID is required."));
        }

        var written = await credentialStore.WriteRawAsync(request.CredentialsJson, cancellationToken).ConfigureAwait(false);
        if (written.IsFailure)
        {
            return Result<TodoSettings>.Failure(written.Error!);
        }

        var settings = new TodoSettings
        {
            SpreadsheetId = request.SpreadsheetId.Trim(),
            DefaultSpace = request.DefaultSpace.Trim(),
        };

        var saved = await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return Result<TodoSettings>.Failure(saved.Error!);
        }

        var probeResult = await probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (!probeResult.TryGetValue(out _))
        {
            return Result<TodoSettings>.Failure(probeResult.Error!);
        }

        var ensured = await spaceService.EnsureExistsAsync(SpaceName.Create(settings.DefaultSpace), cancellationToken)
            .ConfigureAwait(false);

        return ensured.IsSuccess
            ? Result<TodoSettings>.Success(settings)
            : Result<TodoSettings>.Failure(ensured.Error!);
    }
}

public sealed class DiagnosticsService(ISettingsStore settingsStore, ICredentialStore credentialStore, IBackendProbe probe)
{
    public async Task<Result<DiagnosticsReport>> RunAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.TryGetValue(out var loaded))
        {
            return Result<DiagnosticsReport>.Failure(settings.Error!);
        }

        var credentials = await credentialStore.ReadRawAsync(cancellationToken).ConfigureAwait(false);
        if (credentials.IsFailure)
        {
            return Result<DiagnosticsReport>.Failure(credentials.Error!);
        }

        var probed = await probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (!probed.TryGetValue(out var result))
        {
            return Result<DiagnosticsReport>.Failure(probed.Error!);
        }

        return Result<DiagnosticsReport>.Success(new DiagnosticsReport
        {
            Settings = loaded,
            SettingsLocation = settingsStore.DescribeLocation(),
            CredentialsLocation = credentialStore.DescribeLocation(),
            SpreadsheetTitle = result.SpreadsheetTitle,
            Tabs = result.Tabs,
            SchemaVersion = result.SchemaVersion,
            ServiceAccountEmail = result.ServiceAccountEmail,
            SchemaReady = result.SchemaReady,
        });
    }

    public Task<Result<BackendRepairResult>> RepairAsync(CancellationToken cancellationToken) =>
        probe.RepairAsync(cancellationToken);
}

public sealed record DiagnosticsReport
{
    public required TodoSettings Settings { get; init; }

    public required string SettingsLocation { get; init; }

    public required string CredentialsLocation { get; init; }

    public required string SpreadsheetTitle { get; init; }

    public required IReadOnlyList<string> Tabs { get; init; }

    public required int SchemaVersion { get; init; }

    public string? ServiceAccountEmail { get; init; }

    public required bool SchemaReady { get; init; }
}
