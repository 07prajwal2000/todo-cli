namespace Todo.Domain.Tasks;

/// <summary>
/// Owns the derived state that hangs off a status change. Keeping this out of the services
/// means completed_at can never be forgotten or set inconsistently by one caller.
/// </summary>
public static class TaskTransitionPolicy
{
    public static bool IsAllowed(TaskState from, TaskState to) => StatusTransitions.CanTransition(from, to);

    public static string DescribeRejection(TaskState from, TaskState to) =>
        $"Cannot move a task from {TaskStates.Canonical(from)} to {TaskStates.Canonical(to)}. " +
        $"From {TaskStates.Canonical(from)} you may move to: {StatusTransitions.DescribeAllowedFrom(from)}.";

    public static TaskItem Apply(TaskItem task, TaskState target, DateTimeOffset now)
    {
        if (task.Status == target)
        {
            return task;
        }

        return target == TaskState.Completed
            ? task with { Status = target, CompletedAt = now }
            : task with { Status = target, CompletedAt = null };
    }
}
