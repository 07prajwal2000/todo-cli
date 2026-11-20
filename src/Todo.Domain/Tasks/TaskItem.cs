namespace Todo.Domain.Tasks;

public sealed record TaskItem
{
    public required TaskId Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public TaskState Status { get; init; } = TaskState.Pending;

    public DueDate? DueOn { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string Remarks { get; init; } = string.Empty;

    public bool IsOpen => Status is not TaskState.Completed;
}
