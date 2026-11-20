using Todo.Application.Common;
using Todo.Application.Notes;
using Todo.Application.Spaces;
using Todo.Application.Tasks;
using Todo.Domain.Notes;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Web.Tests;

/// <summary>In-memory <see cref="ITaskRepository"/> so tests never touch a real sheet.</summary>
internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly Dictionary<SpaceName, List<TaskItem>> _tasksBySpace = [];
    private readonly Lock _gate = new();
    private int _nextId;

    public int AddCalls { get; private set; }

    public Task<Result<IReadOnlyList<TaskItem>>> ListAsync(SpaceName space, CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<TaskItem>>.Success([.. Store(space).OrderBy(task => task.Id.Value)]));

    public Task<Result<TaskItem>> FindAsync(SpaceName space, TaskId id, CancellationToken cancellationToken)
    {
        var found = Store(space).FirstOrDefault(task => task.Id == id);

        return Task.FromResult(found is null
            ? Result<TaskItem>.Failure(TodoError.NotFound($"Task {id.Value} was not found."))
            : Result<TaskItem>.Success(found));
    }

    public Task<Result<TaskItem>> AddAsync(SpaceName space, TaskDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        AddCalls++;

        var store = Store(space);
        var task = new TaskItem
        {
            Id = new TaskId(++_nextId),
            Name = draft.Name,
            Description = draft.Description,
            Status = draft.Status,
            DueOn = draft.DueOn,
            CreatedAt = draft.CreatedAt,
            Remarks = draft.Remarks,
        };

        store.Add(task);
        return Task.FromResult(Result<TaskItem>.Success(task));
    }

    public Task<Result<TaskItem>> UpdateAsync(SpaceName space, TaskItem task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        var store = Store(space);
        var index = store.FindIndex(candidate => candidate.Id == task.Id);

        if (index < 0)
        {
            return Task.FromResult(Result<TaskItem>.Failure(TodoError.NotFound($"Task {task.Id.Value} was not found.")));
        }

        store[index] = task;
        return Task.FromResult(Result<TaskItem>.Success(task));
    }

    public Task<Result> DeleteAsync(SpaceName space, TaskId id, CancellationToken cancellationToken)
    {
        var store = Store(space);
        var removed = store.RemoveAll(task => task.Id == id);

        return Task.FromResult(removed > 0
            ? Result.Success()
            : Result.Failure(TodoError.NotFound($"Task {id.Value} was not found.")));
    }

    public void Seed(SpaceName space, params TaskItem[] tasks)
    {
        var store = Store(space);

        foreach (var task in tasks)
        {
            store.Add(task);
            _nextId = Math.Max(_nextId, task.Id.Value);
        }
    }

    public IReadOnlyList<TaskItem> Snapshot(SpaceName space) => [.. Store(space)];

    private List<TaskItem> Store(SpaceName space)
    {
        lock (_gate)
        {
            if (!_tasksBySpace.TryGetValue(space, out var store))
            {
                store = [];
                _tasksBySpace[space] = store;
            }

            return store;
        }
    }
}

internal sealed class InMemoryNoteRepository : INoteRepository
{
    private readonly List<Note> _notes = [];

    public IReadOnlyList<Note> Snapshot() => [.. _notes];

    public Task<Result<IReadOnlyList<Note>>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<Note>>.Success([.. _notes]));

    public Task<Result<Note>> FindAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_notes.FirstOrDefault(note => note.Id == id) is { } found
            ? Result<Note>.Success(found)
            : TodoError.NotFound($"Note {id} was not found."));

    public Task<Result<Note>> AddAsync(Note draft, CancellationToken cancellationToken)
    {
        var note = draft with { Id = _notes.Select(existing => existing.Id).DefaultIfEmpty(0).Max() + 1 };
        _notes.Add(note);
        return Task.FromResult(Result<Note>.Success(note));
    }

    public Task<Result<Note>> UpdateAsync(Note note, CancellationToken cancellationToken)
    {
        var index = _notes.FindIndex(existing => existing.Id == note.Id);
        if (index < 0)
        {
            return Task.FromResult<Result<Note>>(TodoError.NotFound($"Note {note.Id} was not found."));
        }

        _notes[index] = note;
        return Task.FromResult(Result<Note>.Success(note));
    }

    public Task<Result> DeleteAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_notes.RemoveAll(note => note.Id == id) > 0
            ? Result.Success()
            : Result.Failure(TodoError.NotFound($"Note {id} was not found.")));
}

internal sealed class InMemorySpaceRepository : ISpaceRepository
{
    private readonly Dictionary<SpaceName, bool> _spaces = new()
    {
        [SpaceName.Default] = true,
    };

    private readonly Lock _gate = new();

    public Task<Result<IReadOnlyList<SpaceName>>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<Result<IReadOnlyList<SpaceName>>>(_spaces.Keys.ToList());
        }
    }

    public Task<Result<bool>> ExistsAsync(SpaceName space, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<Result<bool>>(_spaces.ContainsKey(space));
        }
    }

    public Task<Result> CreateAsync(SpaceName space, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _spaces[space] = true;
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteAsync(SpaceName space, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_spaces.Remove(space))
            {
                return Task.FromResult(Result.Failure(TodoError.NotFound($"Space '{space.Value}' was not found.")));
            }
        }

        return Task.FromResult(Result.Success());
    }
}

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
