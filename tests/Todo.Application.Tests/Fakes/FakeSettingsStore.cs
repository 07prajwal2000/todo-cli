using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Application.Tests.Fakes;

/// <summary>Counts save calls and can be told to fail so the setup flow's error path is testable.</summary>
public sealed class FakeSettingsStore : ISettingsStore
{
    public int SaveCalls { get; private set; }

    public int LoadCalls { get; private set; }

    public TodoSettings? Saved { get; private set; }

    public TodoError? SaveFailure { get; set; }

    public TodoSettings Settings { get; set; } = new();

    public Task<Result<TodoSettings>> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        return Task.FromResult(Result<TodoSettings>.Success(Settings));
    }

    public Task<Result> SaveAsync(TodoSettings settings, CancellationToken cancellationToken)
    {
        SaveCalls++;

        if (SaveFailure is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        Saved = settings;
        Settings = settings;
        return Task.FromResult(Result.Success());
    }

    public Task<Result<bool>> ExistsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<bool>.Success(Saved is not null));

    public string DescribeLocation() => "/fake/config.json";
}
