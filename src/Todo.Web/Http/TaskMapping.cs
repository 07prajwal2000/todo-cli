using Todo.Domain.Tasks;
using Todo.Web.Contracts;

namespace Todo.Web.Http;

/// <summary>
/// Domain to wire projection. Kept in one place so the JSON contract of a task is defined once and
/// cannot drift between the list and single-item endpoints.
/// </summary>
internal static class TaskMapping
{
    internal static TaskDto ToDto(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new TaskDto
        {
            Id = task.Id.Value,
            Name = task.Name,
            Description = task.Description,
            Status = TaskStates.Canonical(task.Status),
            DueOn = task.DueOn?.ToString(),
            CreatedAt = task.CreatedAt.UtcDateTime,
            CompletedAt = task.CompletedAt?.UtcDateTime,
            Remarks = task.Remarks,
        };
    }

    internal static IReadOnlyList<TaskDto> ToDtos(IReadOnlyList<TaskItem> tasks) => [.. tasks.Select(ToDto)];
}
