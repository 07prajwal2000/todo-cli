using System.Globalization;
using Todo.Application.Common;
using Todo.Domain.Tasks;

namespace Todo.Infrastructure.GoogleSheets.Mapping;

/// <summary>
/// Resolves schema column names to positions in a concrete header row. Naming happens through this
/// binding rather than raw offsets so a user who reorders columns in the sheet keeps working — only
/// the columns that disappear are treated as an error.
/// </summary>
public sealed class TaskSchemaBinding
{
    private readonly Dictionary<string, int> _indexByColumn;

    private TaskSchemaBinding(Dictionary<string, int> indexByColumn, IReadOnlyList<string?> headerCells)
    {
        _indexByColumn = indexByColumn;
        HeaderCells = headerCells;
    }

    /// <summary>Identity binding used when the row is already in <see cref="TaskSchema.Columns"/> order.</summary>
    public static TaskSchemaBinding Positional { get; } = CreatePositional();

    public IReadOnlyList<string?> HeaderCells { get; }

    public IReadOnlyCollection<string> Columns => _indexByColumn.Keys;

    public bool Contains(string column) => _indexByColumn.ContainsKey(Normalize(column));

    public int? IndexOf(string column) =>
        _indexByColumn.TryGetValue(Normalize(column), out var index) ? index : null;

    /// <summary>Returns the cell for a column, or null when the row is shorter than the header.</summary>
    public object? ValueAt(IReadOnlyList<object?> cells, string column)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var index = IndexOf(column);
        return index is { } position && position < cells.Count ? cells[position] : null;
    }

    public string? TextAt(IReadOnlyList<object?> cells, string column)
    {
        var value = ValueAt(cells, column);
        return value switch
        {
            null => null,
            string text => text,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }

    public static Result<TaskSchemaBinding> ValidateHeaders(IReadOnlyList<object?> headerCells)
    {
        ArgumentNullException.ThrowIfNull(headerCells);

        var indexByColumn = new Dictionary<string, int>(TaskSchema.Columns.Count, StringComparer.OrdinalIgnoreCase);
        var names = new List<string?>(headerCells.Count);

        for (var position = 0; position < headerCells.Count; position++)
        {
            var name = (headerCells[position] as string)?.Trim();
            names.Add(name);

            if (!string.IsNullOrEmpty(name))
            {
                indexByColumn.TryAdd(name, position);
            }
        }

        foreach (var required in TaskSchema.RequiredColumns)
        {
            if (indexByColumn.ContainsKey(required))
            {
                continue;
            }

            return Result<TaskSchemaBinding>.Failure(TodoError.Conflict(
                $"The sheet is missing required column '{required}'. Run 'todo doctor --fix'."));
        }

        return Result<TaskSchemaBinding>.Success(new TaskSchemaBinding(indexByColumn, names));
    }

    public static TaskSchemaBinding FromHeaders(IReadOnlyList<object?> headerCells)
    {
        var validated = ValidateHeaders(headerCells);
        return validated.IsSuccess ? validated.Value : Positional;
    }

    private static string Normalize(string column) => column.Trim();

    private static TaskSchemaBinding CreatePositional()
    {
        var map = new Dictionary<string, int>(TaskSchema.Columns.Count, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < TaskSchema.Columns.Count; index++)
        {
            map[TaskSchema.Columns[index]] = index;
        }

        return new TaskSchemaBinding(map, TaskSchema.Columns);
    }
}
