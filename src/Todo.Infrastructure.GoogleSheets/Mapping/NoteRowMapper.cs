using System.Globalization;
using Todo.Application.Common;
using Todo.Domain.Notes;

namespace Todo.Infrastructure.GoogleSheets.Mapping;

/// <summary>Cell conversion for the notes tab, in <see cref="NoteSchema.Columns"/> order.</summary>
public static class NoteRowMapper
{
    public static IReadOnlyList<object> ToRow(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);

        return
        [
            note.Id.ToString(CultureInfo.InvariantCulture),
            note.Title,
            note.Content,
            TaskRowMapper.FormatTimestamp(note.CreatedAt),
        ];
    }

    public static Result<Note> FromRow(IReadOnlyList<object?> cells, int rowNumber)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var where = $"Notes row {rowNumber.ToString(CultureInfo.InvariantCulture)}";
        var rawId = SheetsGateway.AsText(cells, 0)?.Trim();

        if (!int.TryParse(rawId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            return TodoError.Conflict($"{where} has an invalid note id '{rawId}'. Ids must be positive whole numbers.");
        }

        var rawCreated = SheetsGateway.AsText(cells, 3);
        if (!TaskRowMapper.TryParseTimestamp(rawCreated, out var createdAt))
        {
            return TodoError.Conflict($"{where} has an invalid created_at value '{rawCreated?.Trim()}'. Expected an ISO-8601 timestamp.");
        }

        return new Note
        {
            Id = id,
            Title = SheetsGateway.AsText(cells, 1)?.Trim() ?? string.Empty,
            Content = SheetsGateway.AsText(cells, 2) ?? string.Empty,
            CreatedAt = createdAt,
        };
    }
}
