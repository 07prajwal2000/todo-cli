using Todo.Application.Common;

namespace Todo.Application.Setup;

/// <summary>
/// Backend selection is intentionally a single value rather than a plugin registry: only
/// google-sheets ships today, and the seam exists so a second adapter is a small change.
/// </summary>
public static class ProviderNames
{
    public const string GoogleSheets = "google-sheets";
}

public sealed record TodoSettings
{
    public const int DefaultServerPort = 12000;

    public string Provider { get; init; } = ProviderNames.GoogleSheets;

    public string SpreadsheetId { get; init; } = string.Empty;

    public string DefaultSpace { get; init; } = "default";

    public string CredentialsFile { get; init; } = "credentials.json";

    public int ServerPort { get; init; } = DefaultServerPort;
}

public interface ISettingsStore
{
    Task<Result<TodoSettings>> LoadAsync(CancellationToken cancellationToken);

    Task<Result> SaveAsync(TodoSettings settings, CancellationToken cancellationToken);

    Task<Result<bool>> ExistsAsync(CancellationToken cancellationToken);

    string DescribeLocation();
}

public interface ICredentialStore
{
    Task<Result<string>> ReadRawAsync(CancellationToken cancellationToken);

    Task<Result> WriteRawAsync(string json, CancellationToken cancellationToken);

    string DescribeLocation();
}
