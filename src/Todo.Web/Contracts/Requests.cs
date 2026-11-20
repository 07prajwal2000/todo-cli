namespace Todo.Web.Contracts;

public sealed record CreateTaskRequest
{
    public string? Space { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? DueOn { get; init; }

    public string? Status { get; init; }

    public string? Remarks { get; init; }
}

/// <summary>
/// Every property is optional because the request is a partial update: <c>null</c> means "leave as
/// is", and clearing the due date is expressed with <c>clearDue</c> rather than a null date.
/// </summary>
public sealed record UpdateTaskRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? DueOn { get; init; }

    public bool? ClearDue { get; init; }

    public string? Status { get; init; }

    public string? Remarks { get; init; }
}

public sealed record CreateSpaceRequest
{
    public string? Name { get; init; }
}

public sealed record CreateNoteRequest
{
    public string? Title { get; init; }

    public string? Content { get; init; }
}

/// <summary>Partial update: <c>null</c> leaves the field as is.</summary>
public sealed record UpdateNoteRequest
{
    public string? Title { get; init; }

    public string? Content { get; init; }
}
