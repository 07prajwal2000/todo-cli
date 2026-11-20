namespace Todo.Domain.Tasks;

public static class TaskStates
{
    private static readonly Dictionary<TaskState, string> CanonicalNames = new()
    {
        [TaskState.Backlog] = "backlog",
        [TaskState.Pending] = "pending",
        [TaskState.Ongoing] = "ongoing",
        [TaskState.Completed] = "completed",
    };

    private static readonly Dictionary<string, TaskState> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["backlog"] = TaskState.Backlog,
        ["icebox"] = TaskState.Backlog,
        ["later"] = TaskState.Backlog,
        ["pending"] = TaskState.Pending,
        ["open"] = TaskState.Pending,
        ["todo"] = TaskState.Pending,
        ["ongoing"] = TaskState.Ongoing,
        ["doing"] = TaskState.Ongoing,
        ["wip"] = TaskState.Ongoing,
        ["started"] = TaskState.Ongoing,
        ["completed"] = TaskState.Completed,
        ["complete"] = TaskState.Completed,
        ["done"] = TaskState.Completed,
        ["closed"] = TaskState.Completed,
    };

    public static IReadOnlyList<TaskState> All { get; } =
        [TaskState.Pending, TaskState.Ongoing, TaskState.Backlog, TaskState.Completed];

    public static string Canonical(TaskState state) => CanonicalNames[state];

    public static bool TryParse(string? value, out TaskState state)
    {
        state = default;
        return !string.IsNullOrWhiteSpace(value) && Aliases.TryGetValue(value.Trim(), out state);
    }

    public static string DescribeAccepted() => string.Join(", ", All.Select(Canonical));
}
