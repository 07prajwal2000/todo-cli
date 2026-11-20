namespace Todo.Web.Contracts;

/// <summary>Wire shape for a task. Deliberately separate from <c>TaskItem</c> so the domain never leaks.</summary>
public sealed record TaskDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? DueOn { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public string Remarks { get; init; } = string.Empty;
}
