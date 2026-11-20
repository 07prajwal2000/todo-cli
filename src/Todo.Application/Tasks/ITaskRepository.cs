using Todo.Application.Common;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Application.Tasks;

public interface ITaskRepository
{
    Task<Result<IReadOnlyList<TaskItem>>> ListAsync(SpaceName space, CancellationToken cancellationToken);

    Task<Result<TaskItem>> FindAsync(SpaceName space, TaskId id, CancellationToken cancellationToken);

    Task<Result<TaskItem>> AddAsync(SpaceName space, TaskDraft draft, CancellationToken cancellationToken);

    Task<Result<TaskItem>> UpdateAsync(SpaceName space, TaskItem task, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(SpaceName space, TaskId id, CancellationToken cancellationToken);
}

public sealed record TaskDraft
{
    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public required TaskState Status { get; init; }

    public DueDate? DueOn { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string Remarks { get; init; } = string.Empty;
}
