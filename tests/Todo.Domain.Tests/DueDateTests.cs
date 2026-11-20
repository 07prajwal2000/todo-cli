using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class DueDateTests
{
    [Fact]
    public void TryParseAcceptsTheCanonicalFormat()
    {
        Assert.True(DueDate.TryParse("2026-12-01", out var dueDate));

        Assert.Equal(new DateOnly(2026, 12, 1), dueDate.Value);
    }

    [Theory]
    [InlineData("2026-12-1")]
    [InlineData("01/12/2026")]
    [InlineData("not-a-date")]
    [InlineData("2026-13-01")]
    [InlineData("2026-12-32")]
    [InlineData("20261201")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParseRejectsAnythingButTheCanonicalFormat(string? value)
    {
        Assert.False(DueDate.TryParse(value, out var dueDate));
        Assert.Equal(default(DueDate), dueDate);
    }

    [Fact]
    public void TryParseTrimsSurroundingWhitespace()
    {
        Assert.True(DueDate.TryParse("  2026-12-01	", out var dueDate));
        Assert.Equal(new DateOnly(2026, 12, 1), dueDate.Value);
    }

    [Fact]
    public void ToStringRoundTripsThroughTryParse()
    {
        var original = DueDate.From(new DateOnly(2026, 2, 9));

        Assert.Equal("2026-02-09", original.ToString());
        Assert.True(DueDate.TryParse(original.ToString(), out var parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void FromWrapsTheDateOnlyValue()
    {
        var value = new DateOnly(2026, 3, 4);
        var dueDate = DueDate.From(value);

        Assert.Equal(value, dueDate.Value);
        Assert.Equal(new DueDate(value), dueDate);
    }

    [Fact]
    public void OrderingOperatorsCompareTheUnderlyingDate()
    {
        var earlier = DueDate.From(new DateOnly(2026, 1, 1));
        var later = DueDate.From(new DateOnly(2026, 6, 15));

        Assert.True(earlier < later);
        Assert.True(earlier <= later);
        Assert.True(later > earlier);
        Assert.True(later >= earlier);
        Assert.False(later < earlier);
        Assert.True(earlier <= DueDate.From(new DateOnly(2026, 1, 1)));
        Assert.True(earlier >= DueDate.From(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void CompareToOrdersChronologically()
    {
        var earlier = DueDate.From(new DateOnly(2026, 1, 1));
        var later = DueDate.From(new DateOnly(2026, 6, 15));

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(later.CompareTo(earlier) > 0);
        Assert.Equal(0, later.CompareTo(later));
    }
}
