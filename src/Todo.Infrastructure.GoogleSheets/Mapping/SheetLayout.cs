using System.Globalization;
using System.Text;
using Todo.Domain.Tasks;

namespace Todo.Infrastructure.GoogleSheets.Mapping;

/// <summary>
/// Pure translation between the schema and A1 notation. Row indexes are always 1-based because
/// that is what A1 ranges and the delete-dimension offsets are expressed in; keeping the
/// conversion here means no caller has to remember the off-by-one.
/// </summary>
public sealed class SheetLayout
{
    /// <summary>Google Sheets allows 16 384 columns, which is the last legitimate A1 column.</summary>
    private const int MaxColumnNumber = 16384;

    private const string ReadOnlyColumns = "A:Z";

    private SheetLayout(string tabName, IReadOnlyList<string> columns)
    {
        if (string.IsNullOrWhiteSpace(tabName))
        {
            throw new ArgumentException("A sheet title is required.", nameof(tabName));
        }

        ArgumentNullException.ThrowIfNull(columns);
        ArgumentOutOfRangeException.ThrowIfZero(columns.Count, nameof(columns));

        TabName = tabName;
        Columns = columns;
    }

    public string TabName { get; }

    public IReadOnlyList<string> Columns { get; }

    public int ColumnCount => Columns.Count;

    public string QuotedTabName => QuoteTitle(TabName);

    public string LastColumnLetter => ColumnLetter(ColumnCount);

    public string HeaderRange => $"{QuotedTabName}!A1:{LastColumnLetter}1";

    public string DataRange => $"{QuotedTabName}!A2:{LastColumnLetter}";

    public string TailRange => $"{QuotedTabName}!A:{LastColumnLetter}";

    /// <summary>
    /// Reads one column past the schema so an accidentally added column shows up as data rather
    /// than silently truncating a row.
    /// </summary>
    public string ReadRange => $"{QuotedTabName}!{ReadOnlyColumns}";

    public static SheetLayout ForSchema(string tabName) => new(tabName, TaskSchema.Columns);

    public static SheetLayout ForTab(string tabName, IReadOnlyList<string> columns) => new(tabName, columns);

    public static SheetLayout ForMetaTab => new(
        TaskSchema.MetaTabName,
        [TaskSchema.MetaKeyColumn, TaskSchema.MetaValueColumn]);

    /// <summary>
    /// Always quotes the title. Single-quoting is legal for every tab name Google accepts and
    /// removes the need to sniff for spaces or reserved characters at each call site.
    /// </summary>
    public static string QuoteTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A sheet title is required.", nameof(title));
        }

        return $"'{title.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    public static string ColumnLetter(int columnNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnNumber, 1, nameof(columnNumber));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(columnNumber, MaxColumnNumber, nameof(columnNumber));

        var builder = new StringBuilder(3);
        var remaining = columnNumber;

        while (remaining > 0)
        {
            var offset = (remaining - 1) % 26;
            builder.Insert(0, (char)('A' + offset));
            remaining = (remaining - 1) / 26;
        }

        return builder.ToString();
    }

    public string RowRange(int rowNumber) => RowRange(rowNumber, ColumnCount);

    public string RowRange(int rowNumber, int columnCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowNumber, 1, nameof(rowNumber));
        ArgumentOutOfRangeException.ThrowIfLessThan(columnCount, 1, nameof(columnCount));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{QuotedTabName}!A{rowNumber}:{ColumnLetter(columnCount)}{rowNumber}");
    }

    public string TailRangeTo(int columnCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnCount, 1, nameof(columnCount));

        return $"{QuotedTabName}!A:{ColumnLetter(columnCount)}";
    }

    public override string ToString() => QuotedTabName;
}
