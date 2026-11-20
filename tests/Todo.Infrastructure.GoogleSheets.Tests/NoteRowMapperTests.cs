using Todo.Application.Common;
using Todo.Domain.Notes;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets.Tests;

public sealed class NoteRowMapperTests
{
    private static readonly Note Sample = new()
    {
        Id = 4,
        Title = "Standup",
        Content = "# Heading\n=SUM(A1)\n- a\n- b",
        CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
    };

    [Fact]
    public void RowFollowsTheSchemaColumnOrder()
    {
        var row = NoteRowMapper.ToRow(Sample);

        Assert.Equal(NoteSchema.Columns.Count, row.Count);
        Assert.Equal(["4", "Standup", Sample.Content, "2026-01-02T03:04:05Z"], row.Select(cell => (string)cell));
    }

    [Fact]
    public void RoundTripPreservesMultilineMarkdownExactly()
    {
        var mapped = NoteRowMapper.FromRow([.. NoteRowMapper.ToRow(Sample)], 2);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(Sample, mapped.Value);
    }

    [Fact]
    public void ToleratesNumericIdsAndShortRows()
    {
        var mapped = NoteRowMapper.FromRow([7d, "Short", null, "2026-01-02T03:04:05Z"], 3);

        Assert.Equal(7, mapped.Value.Id);
        Assert.Equal(string.Empty, mapped.Value.Content);
    }

    [Theory]
    [InlineData("abc", "2026-01-02T03:04:05Z")]
    [InlineData("0", "2026-01-02T03:04:05Z")]
    [InlineData("1", "yesterday-ish")]
    [InlineData("1", "")]
    public void BadIdOrTimestampIsAConflictNamingTheRow(string id, string created)
    {
        var mapped = NoteRowMapper.FromRow([id, "t", "c", created], 9);

        Assert.True(mapped.IsFailure);
        Assert.Equal(ErrorCode.Conflict, mapped.Error!.Code);
        Assert.Contains("row 9", mapped.Error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
