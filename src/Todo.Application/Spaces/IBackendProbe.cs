using Todo.Application.Common;

namespace Todo.Application.Spaces;

public interface IBackendProbe
{
    Task<Result<BackendProbeResult>> ProbeAsync(CancellationToken cancellationToken);

    Task<Result<BackendRepairResult>> RepairAsync(CancellationToken cancellationToken);
}

public sealed record BackendProbeResult
{
    public required string SpreadsheetTitle { get; init; }

    public required IReadOnlyList<string> Tabs { get; init; }

    public required int SchemaVersion { get; init; }

    public string? ServiceAccountEmail { get; init; }

    public required bool SchemaReady { get; init; }
}

public sealed record BackendRepairResult
{
    public required IReadOnlyList<string> Actions { get; init; }
}
