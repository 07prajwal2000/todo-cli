using Todo.Domain.Tasks;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets.Tests;

public sealed class SheetLayoutTests
{
    [Theory]
    [InlineData(1, "A")]
    [InlineData(2, "B")]
    [InlineData(8, "H")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(28, "AB")]
    [InlineData(52, "AZ")]
    [InlineData(53, "BA")]
    [InlineData(702, "ZZ")]
    [InlineData(703, "AAA")]
    [InlineData(16384, "XFD")]
    public void ColumnLetter_maps_column_numbers_to_a1_letters(int columnNumber, string expected)
    {
        Assert.Equal(expected, SheetLayout.ColumnLetter(columnNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(16385)]
    public void ColumnLetter_rejects_out_of_range_columns(int columnNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetLayout.ColumnLetter(columnNumber));
    }

    [Theory]
    [InlineData("default", "'default'")]
    [InlineData("my tasks", "'my tasks'")]
    [InlineData("work/2026", "'work/2026'")]
    [InlineData("o'brien", "'o''brien'")]
    public void QuoteTitle_wraps_the_title_in_single_quotes(string title, string expected)
    {
        Assert.Equal(expected, SheetLayout.QuoteTitle(title));
    }

    [Fact]
    public void ForSchema_builds_schema_aligned_ranges()
    {
        var layout = SheetLayout.ForSchema("my tasks");

        Assert.Equal("my tasks", layout.TabName);
        Assert.Equal("'my tasks'", layout.QuotedTabName);
        Assert.Equal(TaskSchema.Columns.Count, layout.ColumnCount);
        Assert.Equal("H", layout.LastColumnLetter);
        Assert.Equal("'my tasks'!A1:H1", layout.HeaderRange);
        Assert.Equal("'my tasks'!A2:H", layout.DataRange);
        Assert.Equal("'my tasks'!A:H", layout.TailRange);
        Assert.Equal("'my tasks'!A:Z", layout.ReadRange);
    }

    [Fact]
    public void RowRange_targets_a_single_row()
    {
        var layout = SheetLayout.ForSchema("default");

        Assert.Equal("'default'!A2:H2", layout.RowRange(2));
        Assert.Equal("'default'!A17:H17", layout.RowRange(17));
    }

    [Fact]
    public void RowRange_honours_a_narrower_column_count()
    {
        var layout = SheetLayout.ForSchema("default");

        Assert.Equal("'default'!A5:A5", layout.RowRange(5, 1));
        Assert.Equal("'default'!A5:B5", layout.RowRange(5, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void RowRange_rejects_non_positive_rows(int rowNumber)
    {
        var layout = SheetLayout.ForSchema("default");

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.RowRange(rowNumber));
    }

    [Fact]
    public void ForMetaTab_uses_the_two_meta_columns()
    {
        var layout = SheetLayout.ForMetaTab;

        Assert.Equal(TaskSchema.MetaTabName, layout.TabName);
        Assert.Equal("'_meta'!A1:B1", layout.HeaderRange);
        Assert.Equal("'_meta'!A2:B", layout.DataRange);
        Assert.Equal("'_meta'!A:B", layout.TailRange);
        Assert.Equal("'_meta'!A7:B7", layout.RowRange(7));
    }

    [Fact]
    public void TailRangeTo_widens_beyond_the_schema()
    {
        var layout = SheetLayout.ForSchema("default");

        Assert.Equal("'default'!A:AA", layout.TailRangeTo(27));
    }

    [Fact]
    public void ForTab_uses_the_supplied_columns()
    {
        var layout = SheetLayout.ForTab("scratch", ["id", "name"]);

        Assert.Equal(2, layout.ColumnCount);
        Assert.Equal("'scratch'!A1:B1", layout.HeaderRange);
    }

    [Fact]
    public void ForSchema_rejects_a_blank_title()
    {
        Assert.Throws<ArgumentException>(() => SheetLayout.ForSchema("  "));
    }
}
