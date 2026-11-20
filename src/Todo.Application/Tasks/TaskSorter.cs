using Todo.Domain.Tasks;

namespace Todo.Application.Tasks;

public static class TaskSorter
{
    private static readonly Dictionary<TaskState, int> StatePriority = new()
    {
        [TaskState.Ongoing] = 0,
        [TaskState.Pending] = 1,
        [TaskState.Backlog] = 2,
        [TaskState.Completed] = 3,
    };

    /// <summary>
    /// Default ordering is the one that makes <c>todo ls</c> useful: active work first, then by
    /// how soon it is due (undated last), then by id for a stable tiebreak.
    /// </summary>
    public static IReadOnlyList<TaskItem> Sort(IEnumerable<TaskItem> tasks, TaskSortField sortBy) =>
        sortBy switch
        {
            TaskSortField.Due => [.. tasks.OrderBy(t => t.DueOn is null).ThenBy(t => t.DueOn).ThenBy(t => t.Id)],
            TaskSortField.Id => [.. tasks.OrderBy(t => t.Id)],
            TaskSortField.Status => [.. tasks.OrderBy(t => StatePriority[t.Status]).ThenBy(t => t.Id)],
            TaskSortField.Created => [.. tasks.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)],
            _ => SortDefault(tasks),
        };

    private static List<TaskItem> SortDefault(IEnumerable<TaskItem> tasks) =>
        [.. tasks
            .OrderBy(t => StatePriority[t.Status])
            .ThenBy(t => t.DueOn is null)
            .ThenBy(t => t.DueOn)
            .ThenBy(t => t.Id)];
}
