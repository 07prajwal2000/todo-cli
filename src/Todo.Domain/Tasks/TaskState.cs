namespace Todo.Domain.Tasks;

/// <summary>
/// Named <c>TaskState</c> rather than <c>TaskStatus</c> to avoid colliding with
/// <see cref="System.Threading.Tasks.TaskStatus"/> in every consumer.
/// </summary>
public enum TaskState
{
    Backlog,
    Pending,
    Ongoing,
    Completed,
}
