using Todo.Domain.Common;

namespace Todo.Domain.Tasks;

/// <summary>
/// Field-keyed validation shared by add/edit in both the CLI and the dashboard, so the two
/// surfaces cannot disagree about what a valid task looks like.
/// </summary>
public static class TaskValidator
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxRemarksLength = 4000;

    public static FieldErrors? ValidateForCreate(
        string? name,
        string? description,
        string? remarks,
        DueDate? dueOn)
    {
        var errors = new FieldErrorsBuilder();
        ValidateName(errors, name);
        ValidateDescription(errors, description);
        ValidateRemarks(errors, remarks);
        ValidateDueOn(errors, dueOn);
        return errors.Build();
    }

    public static FieldErrors? ValidateForUpdate(
        string? name,
        string? description,
        string? remarks,
        DueDate? dueOn)
    {
        // A partial update only checks fields that were actually supplied.
        var errors = new FieldErrorsBuilder();

        if (name is not null)
        {
            ValidateName(errors, name);
        }

        if (description is not null)
        {
            ValidateDescription(errors, description);
        }

        if (remarks is not null)
        {
            ValidateRemarks(errors, remarks);
        }

        if (dueOn is not null)
        {
            ValidateDueOn(errors, dueOn);
        }

        return errors.Build();
    }

    private static void ValidateName(FieldErrorsBuilder errors, string? name)
    {
        var trimmed = name?.Trim();

        errors.AddIf(string.IsNullOrEmpty(trimmed), TaskSchema.NameColumn, "Name is required.");
        errors.AddIf(trimmed is { Length: > MaxNameLength }, TaskSchema.NameColumn,
            $"Name must be at most {MaxNameLength} characters.");
    }

    private static void ValidateDescription(FieldErrorsBuilder errors, string? description) =>
        errors.AddIf(description is { Length: > MaxDescriptionLength }, TaskSchema.DescriptionColumn,
            $"Description must be at most {MaxDescriptionLength} characters.");

    private static void ValidateRemarks(FieldErrorsBuilder errors, string? remarks) =>
        errors.AddIf(remarks is { Length: > MaxRemarksLength }, TaskSchema.RemarksColumn,
            $"Remarks must be at most {MaxRemarksLength} characters.");

    private static void ValidateDueOn(FieldErrorsBuilder errors, DueDate? dueOn) =>
        errors.AddIf(dueOn is { Value.Year: < 1900 }, TaskSchema.DueOnColumn,
            "Due date must be in the year 1900 or later.");
}
