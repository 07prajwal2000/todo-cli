using Todo.Domain.Common;

namespace Todo.Domain.Notes;

/// <summary>A markdown note. Notes are not scoped to a space: they all live on one <c>_notes</c> tab.</summary>
public sealed record Note
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Single declaration of the notes tab contract and the validation shared by CLI and dashboard.</summary>
public static class NoteSchema
{
    /// <summary>The tab name. The leading underscore keeps it out of the space list, like <c>_meta</c>.</summary>
    public const string TabName = "_notes";

    public const string IdColumn = "id";
    public const string TitleColumn = "title";
    public const string ContentColumn = "content";
    public const string CreatedAtColumn = "created_at";

    public const int MaxTitleLength = 200;

    /// <summary>Google Sheets caps a cell at 50 000 characters.</summary>
    public const int MaxContentLength = 50000;

    public static IReadOnlyList<string> Columns { get; } = [IdColumn, TitleColumn, ContentColumn, CreatedAtColumn];

    public static FieldErrors? Validate(string? title, string? content, bool partial)
    {
        var errors = new FieldErrorsBuilder();

        if (!partial || title is not null)
        {
            var trimmed = title?.Trim();
            errors.AddIf(string.IsNullOrEmpty(trimmed), TitleColumn, "Title is required.");
            errors.AddIf(trimmed is { Length: > MaxTitleLength }, TitleColumn,
                $"Title must be at most {MaxTitleLength} characters.");
        }

        errors.AddIf(content is { Length: > MaxContentLength }, ContentColumn,
            $"Content must be at most {MaxContentLength} characters.");

        return errors.Build();
    }
}
