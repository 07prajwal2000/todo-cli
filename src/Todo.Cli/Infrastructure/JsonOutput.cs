using System.Text.Json;
using System.Text.Json.Serialization;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Cli.Infrastructure;

public sealed record TaskJsonDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Status { get; init; }

    public string? DueOn { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public required string Remarks { get; init; }

    public static TaskJsonDto From(TaskItem task) => new()
    {
        Id = task.Id.Value,
        Name = task.Name,
        Description = task.Description,
        Status = TaskStates.Canonical(task.Status),
        DueOn = task.DueOn?.ToString(),
        CreatedAt = task.CreatedAt,
        CompletedAt = task.CompletedAt,
        Remarks = task.Remarks,
    };
}

public sealed record TaskListJsonDto
{
    public required string Space { get; init; }

    public required IReadOnlyList<TaskJsonDto> Tasks { get; init; }
}

public sealed record SpaceListJsonDto
{
    public required IReadOnlyList<string> Spaces { get; init; }

    public required string DefaultSpace { get; init; }
}

/// <summary>
/// Wire shapes are declared separately from the domain so a rename in the domain cannot silently
/// break someone's script, and so --json output is source-generated rather than reflection-based.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TaskJsonDto))]
[JsonSerializable(typeof(TaskListJsonDto))]
[JsonSerializable(typeof(SpaceListJsonDto))]
public sealed partial class CliJsonContext : JsonSerializerContext;

public static class JsonOutput
{
    public static string TaskList(IReadOnlyList<TaskItem> tasks, SpaceName space) =>
        JsonSerializer.Serialize(
            new TaskListJsonDto
            {
                Space = space.Value,
                Tasks = [.. tasks.Select(TaskJsonDto.From)],
            },
            CliJsonContext.Default.TaskListJsonDto);

    public static string Task(TaskItem task) =>
        JsonSerializer.Serialize(TaskJsonDto.From(task), CliJsonContext.Default.TaskJsonDto);

    public static string Spaces(IReadOnlyList<SpaceName> spaces, string defaultSpace) =>
        JsonSerializer.Serialize(
            new SpaceListJsonDto
            {
                Spaces = [.. spaces.Select(space => space.Value)],
                DefaultSpace = defaultSpace,
            },
            CliJsonContext.Default.SpaceListJsonDto);
}
