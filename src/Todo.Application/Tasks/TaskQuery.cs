using Todo.Domain.Tasks;

namespace Todo.Application.Tasks;

public sealed record TaskQuery
{
    public IReadOnlyList<TaskState> States { get; init; } = [];

    public string? SearchTerm { get; init; }

    public DueDate? DueBefore { get; init; }

    public TaskSortField SortBy { get; init; } = TaskSortField.Default;

    public bool IncludeAllStates { get; init; }
}

public enum TaskSortField
{
    Default,
    Due,
    Id,
    Status,
    Created,
}
