using Todo.Application.Common;
using Todo.Application.Spaces;

namespace Todo.Application.Tests.Fakes;

/// <summary>Counts probes and can be told to fail.</summary>
public sealed class FakeBackendProbe : IBackendProbe
{
    public int ProbeCalls { get; private set; }

    public int RepairCalls { get; private set; }

    public TodoError? ProbeFailure { get; set; }

    public TodoError? RepairFailure { get; set; }

    public IReadOnlyList<string> RepairActions { get; set; } = ["Repaired the header row."];

    public BackendProbeResult Result { get; set; } = new()
    {
        SpreadsheetTitle = "Fake Sheet",
        Tabs = ["default"],
        SchemaVersion = 1,
        SchemaReady = true,
    };

    public Task<Result<BackendProbeResult>> ProbeAsync(CancellationToken cancellationToken)
    {
        ProbeCalls++;

        return Task.FromResult(ProbeFailure is { } error
            ? Result<BackendProbeResult>.Failure(error)
            : Result<BackendProbeResult>.Success(Result));
    }

    public Task<Result<BackendRepairResult>> RepairAsync(CancellationToken cancellationToken)
    {
        RepairCalls++;

        return Task.FromResult(RepairFailure is { } error
            ? Result<BackendRepairResult>.Failure(error)
            : Result<BackendRepairResult>.Success(new BackendRepairResult { Actions = RepairActions }));
    }
}
