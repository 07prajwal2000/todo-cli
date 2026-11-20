using System.Globalization;
using Todo.Application.Common;
using Todo.Domain.Tasks;

namespace Todo.Infrastructure.GoogleSheets.Mapping;

/// <summary>
/// Pure cell conversion. Sheets hands back loosely typed cells — numbers arrive as doubles, blank
/// cells as null, rows may be shorter than the header — so every tolerance the sheet can require is
/// expressed here instead of being scattered through the repository.
/// </summary>
public static class TaskRowMapper
{
    public const string TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";

    public static IReadOnlyList<object> ToRow(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return
        [
            task.Id.Value.ToString(CultureInfo.InvariantCulture),
            task.Name,
            task.Description,
            TaskStates.Canonical(task.Status),
            task.DueOn?.ToString() ?? string.Empty,
            FormatTimestamp(task.CreatedAt),
            task.CompletedAt is { } completedAt ? FormatTimestamp(completedAt) : string.Empty,
            task.Remarks,
        ];
    }

    public static IReadOnlyList<object> HeaderRow() => [.. TaskSchema.Columns.Cast<object>()];

    public static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static Result<TaskItem> FromRow(IReadOnlyList<object?> cells, int? rowNumber) =>
        FromRow(cells, TaskSchemaBinding.Positional, rowNumber);

    public static Result<TaskItem> FromRow(
        IReadOnlyList<object?> cells,
        TaskSchemaBinding binding,
        int? rowNumber)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(binding);

        var where = rowNumber is { } number
            ? $"Row {number.ToString(CultureInfo.InvariantCulture)}"
            : "A sheet row";

        var rawId = binding.TextAt(cells, TaskSchema.IdColumn);
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return Result<TaskItem>.Failure(TodoError.NotFound($"{where} does not have a task id."));
        }

        if (!TryParseId(rawId, out var id))
        {
            return Result<TaskItem>.Failure(TodoError.Conflict(
                $"{where} has an invalid task id '{rawId.Trim()}'. Ids must be whole numbers."));
        }

        var rawStatus = binding.TextAt(cells, TaskSchema.StatusColumn);
        if (!TaskStates.TryParse(rawStatus, out var state))
        {
            var shown = string.IsNullOrWhiteSpace(rawStatus) ? "(blank)" : rawStatus.Trim();
            return Result<TaskItem>.Failure(TodoError.Conflict(
                $"{where} has an unrecognised status '{shown}'. Accepted values: {TaskStates.DescribeAccepted()}."));
        }

        var rawDueOn = binding.TextAt(cells, TaskSchema.DueOnColumn);
        DueDate? dueOn = null;

        if (!string.IsNullOrWhiteSpace(rawDueOn))
        {
            if (!DueDate.TryParse(rawDueOn, out var parsedDue))
            {
                return Result<TaskItem>.Failure(TodoError.Conflict(
                    $"{where} has an invalid due date '{rawDueOn.Trim()}'. Expected {DueDate.Format}."));
            }

            dueOn = parsedDue;
        }

        var rawCreatedAt = binding.TextAt(cells, TaskSchema.CreatedAtColumn);
        if (!TryParseTimestamp(rawCreatedAt, out var createdAt))
        {
            return Result<TaskItem>.Failure(TodoError.Conflict(
                $"{where} has an invalid created_at value '{(rawCreatedAt ?? string.Empty).Trim()}'. Expected an ISO-8601 timestamp."));
        }

        var rawCompletedAt = binding.TextAt(cells, TaskSchema.CompletedAtColumn);
        DateTimeOffset? completedAt = null;

        if (!string.IsNullOrWhiteSpace(rawCompletedAt))
        {
            if (!TryParseTimestamp(rawCompletedAt, out var parsedCompleted))
            {
                return Result<TaskItem>.Failure(TodoError.Conflict(
                    $"{where} has an invalid completed_at value '{rawCompletedAt.Trim()}'. Expected an ISO-8601 timestamp."));
            }

            completedAt = parsedCompleted;
        }

        return Result<TaskItem>.Success(new TaskItem
        {
            Id = id,
            Name = binding.TextAt(cells, TaskSchema.NameColumn)?.Trim() ?? string.Empty,
            Description = binding.TextAt(cells, TaskSchema.DescriptionColumn) ?? string.Empty,
            Status = state,
            DueOn = dueOn,
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            Remarks = binding.TextAt(cells, TaskSchema.RemarksColumn) ?? string.Empty,
        });
    }

    /// <summary>A row whose cells are all empty is padding from the tail read, not a task.</summary>
    public static bool IsBlankRow(IReadOnlyList<object?> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        foreach (var cell in cells)
        {
            if (cell is string text ? !string.IsNullOrWhiteSpace(text) : cell is not null)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParseId(string? value, out TaskId id)
    {
        id = TaskId.None;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            id = new TaskId(parsed);
            return true;
        }

        return false;
    }

    public static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        timestamp = parsed;
        return true;
    }
}
