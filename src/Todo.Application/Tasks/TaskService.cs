using Todo.Application.Common;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Application.Tasks;

public sealed class TaskService(ITaskRepository repository, IClock clock)
{
    public async Task<Result<IReadOnlyList<TaskItem>>> ListAsync(
        SpaceName space,
        TaskQuery query,
        CancellationToken cancellationToken)
    {
        var listed = await repository.ListAsync(space, cancellationToken).ConfigureAwait(false);
        if (!listed.TryGetValue(out var tasks))
        {
            return Result<IReadOnlyList<TaskItem>>.Failure(listed.Error!);
        }

        var filtered = ApplyFilter(tasks, query);
        return Result<IReadOnlyList<TaskItem>>.Success(TaskSorter.Sort(filtered, query.SortBy));
    }

    public Task<Result<TaskItem>> GetAsync(SpaceName space, TaskId id, CancellationToken cancellationToken) =>
        repository.FindAsync(space, id, cancellationToken);

    public async Task<Result<TaskItem>> AddAsync(
        SpaceName space,
        TaskDraftInput input,
        CancellationToken cancellationToken)
    {
        var validation = TaskValidator.ValidateForCreate(
            input.Name,
            input.Description,
            input.Remarks,
            input.DueOn);

        if (validation is not null)
        {
            return Result<TaskItem>.Failure(TodoError.Validation("The task is not valid.", validation.ByField));
        }

        var draft = new TaskDraft
        {
            Name = input.Name!.Trim(),
            Description = input.Description ?? string.Empty,
            Status = input.State,
            DueOn = input.DueOn,
            CreatedAt = clock.UtcNow,
            Remarks = input.Remarks ?? string.Empty,
        };

        return await repository.AddAsync(space, draft, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TaskItem>> EditAsync(
        SpaceName space,
        TaskId id,
        TaskEditInput input,
        CancellationToken cancellationToken)
    {
        var found = await repository.FindAsync(space, id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var task))
        {
            return Result<TaskItem>.Failure(found.Error!);
        }

        var validation = TaskValidator.ValidateForUpdate(
            input.Name,
            input.Description,
            input.Remarks,
            input.DueOn);

        if (validation is not null)
        {
            return Result<TaskItem>.Failure(TodoError.Validation("The task is not valid.", validation.ByField));
        }

        var updated = task;

        if (input.Name is not null)
        {
            updated = updated with { Name = input.Name.Trim() };
        }

        if (input.Description is not null)
        {
            updated = updated with { Description = input.Description };
        }

        if (input.Remarks is not null)
        {
            updated = updated with { Remarks = input.Remarks };
        }

        if (input.ClearDue)
        {
            updated = updated with { DueOn = null };
        }
        else if (input.DueOn is not null)
        {
            updated = updated with { DueOn = input.DueOn };
        }

        if (updated == task)
        {
            return Result<TaskItem>.Success(task);
        }

        return await repository.UpdateAsync(space, updated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TaskItem>> ChangeStateAsync(
        SpaceName space,
        TaskId id,
        TaskState target,
        CancellationToken cancellationToken)
    {
        var found = await repository.FindAsync(space, id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var task))
        {
            return Result<TaskItem>.Failure(found.Error!);
        }

        if (!TaskTransitionPolicy.IsAllowed(task.Status, target))
        {
            return Result<TaskItem>.Failure(
                TodoError.Validation(TaskTransitionPolicy.DescribeRejection(task.Status, target)));
        }

        var transitioned = TaskTransitionPolicy.Apply(task, target, clock.UtcNow);
        if (transitioned == task)
        {
            return Result<TaskItem>.Success(task);
        }

        return await repository.UpdateAsync(space, transitioned, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result> DeleteAsync(SpaceName space, TaskId id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(space, id, cancellationToken);

    private static IEnumerable<TaskItem> ApplyFilter(IReadOnlyList<TaskItem> tasks, TaskQuery query)
    {
        IEnumerable<TaskItem> result = tasks;

        var states = query.States.Count > 0
            ? query.States
            : query.IncludeAllStates ? [] : [TaskState.Pending, TaskState.Ongoing];

        if (states.Count > 0)
        {
            result = result.Where(task => states.Contains(task.Status));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            result = result.Where(task => Matches(task, term));
        }

        if (query.DueBefore is { } dueBefore)
        {
            result = result.Where(task => task.DueOn is { } due && due <= dueBefore);
        }

        return result;
    }

    private static bool Matches(TaskItem task, string term) =>
        task.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || task.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
        || task.Remarks.Contains(term, StringComparison.OrdinalIgnoreCase);
}

public sealed record TaskDraftInput
{
    public required string? Name { get; init; }

    public string? Description { get; init; }

    public TaskState State { get; init; } = TaskState.Pending;

    public DueDate? DueOn { get; init; }

    public string? Remarks { get; init; }
}

public sealed record TaskEditInput
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public DueDate? DueOn { get; init; }

    public bool ClearDue { get; init; }

    public string? Remarks { get; init; }
}
