using System.Text.Json;
using System.Text.Json.Serialization;
using Todo.Domain.Notes;
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

public sealed record NoteJsonDto
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public static NoteJsonDto From(Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
    };
}

public sealed record NoteListJsonDto
{
    public required IReadOnlyList<NoteJsonDto> Notes { get; init; }
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
[JsonSerializable(typeof(NoteJsonDto))]
[JsonSerializable(typeof(NoteListJsonDto))]
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

    public static string Note(Note note) =>
        JsonSerializer.Serialize(NoteJsonDto.From(note), CliJsonContext.Default.NoteJsonDto);

    public static string NoteList(IReadOnlyList<Note> notes) =>
        JsonSerializer.Serialize(
            new NoteListJsonDto { Notes = [.. notes.Select(NoteJsonDto.From)] },
            CliJsonContext.Default.NoteListJsonDto);

    public static string Spaces(IReadOnlyList<SpaceName> spaces, string defaultSpace) =>
        JsonSerializer.Serialize(
            new SpaceListJsonDto
            {
                Spaces = [.. spaces.Select(space => space.Value)],
                DefaultSpace = defaultSpace,
            },
            CliJsonContext.Default.SpaceListJsonDto);
}
