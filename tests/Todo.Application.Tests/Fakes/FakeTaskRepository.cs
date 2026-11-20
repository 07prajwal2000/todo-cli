using Todo.Application.Common;
using Todo.Application.Tasks;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Application.Tests.Fakes;

/// <summary>
/// In-memory task store with one <see cref="TaskItem"/> list per space. Ids are assigned per space
/// the way the Sheets adapter does, and every operation is counted so tests can assert on traffic.
/// </summary>
public sealed class FakeTaskRepository : ITaskRepository
{
    private readonly Dictionary<string, List<TaskItem>> _bySpace = new(StringComparer.Ordinal);

    private readonly Dictionary<string, int> _nextId = new(StringComparer.Ordinal);

    public int ListCalls { get; private set; }

    public int FindCalls { get; private set; }

    public int AddCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    /// <summary>The most recent draft handed to <see cref="AddAsync"/>.</summary>
    public TaskDraft? LastDraft { get; private set; }

    /// <summary>When set, <see cref="ListAsync"/> returns this failure.</summary>
    public TodoError? ListFailure { get; set; }

    /// <summary>When set, <see cref="AddAsync"/> returns this failure.</summary>
    public TodoError? AddFailure { get; set; }

    /// <summary>When set, <see cref="UpdateAsync"/> returns this failure.</summary>
    public TodoError? UpdateFailure { get; set; }

    public IReadOnlyList<TaskItem> TasksIn(SpaceName space) =>
        _bySpace.TryGetValue(space.Value, out var tasks) ? tasks : [];

    public FakeTaskRepository Seed(SpaceName space, params TaskItem[] tasks)
    {
        var stored = StoreFor(space);
        stored.AddRange(tasks);

        var highest = stored.Count == 0 ? 0 : stored.Max(task => task.Id.Value);
        _nextId[space.Value] = highest;

        return this;
    }

    public Task<Result<IReadOnlyList<TaskItem>>> ListAsync(SpaceName space, CancellationToken cancellationToken)
    {
        ListCalls++;

        return Task.FromResult(ListFailure is { } error
            ? Result<IReadOnlyList<TaskItem>>.Failure(error)
            : Result<IReadOnlyList<TaskItem>>.Success([.. TasksIn(space)]));
    }

    public Task<Result<TaskItem>> FindAsync(SpaceName space, TaskId id, CancellationToken cancellationToken)
    {
        FindCalls++;

        var found = TasksIn(space).FirstOrDefault(task => task.Id == id);

        return Task.FromResult(found is null
            ? Result<TaskItem>.Failure(TodoError.NotFound($"Task {id.Value} was not found in space '{space.Value}'."))
            : Result<TaskItem>.Success(found));
    }

    public Task<Result<TaskItem>> AddAsync(SpaceName space, TaskDraft draft, CancellationToken cancellationToken)
    {
        AddCalls++;
        LastDraft = draft;

        if (AddFailure is { } error)
        {
            return Task.FromResult(Result<TaskItem>.Failure(error));
        }

        var stored = StoreFor(space);

        var task = new TaskItem
        {
            Id = new TaskId(_nextId[space.Value] + 1),
            Name = draft.Name,
            Description = draft.Description,
            Status = draft.Status,
            DueOn = draft.DueOn,
            CreatedAt = draft.CreatedAt,
            Remarks = draft.Remarks,
        };

        _nextId[space.Value] = task.Id.Value;
        stored.Add(task);

        return Task.FromResult(Result<TaskItem>.Success(task));
    }

    public Task<Result<TaskItem>> UpdateAsync(SpaceName space, TaskItem task, CancellationToken cancellationToken)
    {
        UpdateCalls++;

        if (UpdateFailure is { } error)
        {
            return Task.FromResult(Result<TaskItem>.Failure(error));
        }

        var stored = StoreFor(space);
        var index = stored.FindIndex(candidate => candidate.Id == task.Id);

        if (index < 0)
        {
            return Task.FromResult(Result<TaskItem>.Failure(
                TodoError.NotFound($"Task {task.Id.Value} was not found in space '{space.Value}'.")));
        }

        stored[index] = task;
        return Task.FromResult(Result<TaskItem>.Success(task));
    }

    public Task<Result> DeleteAsync(SpaceName space, TaskId id, CancellationToken cancellationToken)
    {
        DeleteCalls++;

        var stored = StoreFor(space);
        var index = stored.FindIndex(task => task.Id == id);

        if (index < 0)
        {
            return Task.FromResult(Result.Failure(
                TodoError.NotFound($"Task {id.Value} was not found in space '{space.Value}'.")));
        }

        stored.RemoveAt(index);
        return Task.FromResult(Result.Success());
    }

    private List<TaskItem> StoreFor(SpaceName space)
    {
        if (!_bySpace.TryGetValue(space.Value, out var tasks))
        {
            tasks = [];
            _bySpace[space.Value] = tasks;
            _nextId[space.Value] = 0;
        }

        return tasks;
    }
}
