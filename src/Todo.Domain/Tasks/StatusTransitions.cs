namespace Todo.Domain.Tasks;

public static class StatusTransitions
{
    private static readonly Dictionary<TaskState, TaskState[]> Edges = new()
    {
        [TaskState.Backlog] = [TaskState.Pending, TaskState.Ongoing],
        [TaskState.Pending] = [TaskState.Ongoing, TaskState.Backlog, TaskState.Completed],
        [TaskState.Ongoing] = [TaskState.Completed, TaskState.Pending, TaskState.Backlog],
        [TaskState.Completed] = [TaskState.Pending, TaskState.Ongoing],
    };

    public static IReadOnlyList<TaskState> AllowedFrom(TaskState state) => Edges[state];

    public static bool CanTransition(TaskState from, TaskState to) =>
        from == to || Edges[from].Contains(to);

    public static string DescribeAllowedFrom(TaskState state) =>
        string.Join(", ", Edges[state].Select(TaskStates.Canonical));
}
