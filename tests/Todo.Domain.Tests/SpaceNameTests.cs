using Todo.Domain.Spaces;

namespace Todo.Domain.Tests;

public sealed class SpaceNameTests
{
    [Theory]
    [InlineData("work")]
    [InlineData("my space")]
    [InlineData("a_b")]
    [InlineData("default")]
    [InlineData("Work 2026")]
    [InlineData("o'brien")]
    public void IsValidAcceptsOrdinaryNames(string value)
    {
        Assert.True(SpaceName.IsValid(value, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void IsValidRejectsMissingNames(string? value)
    {
        Assert.False(SpaceName.IsValid(value, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void IsValidRejectsNamesLongerThanTheMaximum()
    {
        var atLimit = new string('a', SpaceName.MaxLength);
        var overLimit = new string('a', SpaceName.MaxLength + 1);

        Assert.True(SpaceName.IsValid(atLimit, out _));

        Assert.False(SpaceName.IsValid(overLimit, out var error));
        Assert.Contains(SpaceName.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), error!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("work/2026")]
    [InlineData("work\\2026")]
    [InlineData("work?")]
    [InlineData("work*")]
    [InlineData("work[1]")]
    [InlineData("work:2026")]
    public void IsValidRejectsSheetTabForbiddenCharacters(string value)
    {
        Assert.False(SpaceName.IsValid(value, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TheNotesTabNameCanNeverCollideWithASpace()
    {
        Assert.False(SpaceName.IsValid(Todo.Domain.Notes.NoteSchema.TabName, out _));
        Assert.True(SpaceName.IsValid("notes", out _));
    }

    [Fact]
    public void IsValidRejectsALeadingUnderscoreBecauseThatPrefixIsReserved()
    {
        Assert.False(SpaceName.IsValid("_meta", out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));

        Assert.True(SpaceName.IsValid("meta_", out _));
    }

    [Theory]
    [InlineData("'work")]
    [InlineData("work'")]
    [InlineData("'work'")]
    public void IsValidRejectsLeadingOrTrailingApostrophes(string value)
    {
        Assert.False(SpaceName.IsValid(value, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("bad/name")]
    [InlineData("_reserved")]
    [InlineData("'quoted'")]
    public void TryCreateReturnsNullForInvalidNames(string? value)
    {
        Assert.Null(SpaceName.TryCreate(value));
    }

    [Theory]
    [InlineData("work", "work")]
    [InlineData("  work  ", "work")]
    [InlineData(" my space ", "my space")]
    public void TryCreateTrimsValidNames(string value, string expected)
    {
        var space = SpaceName.TryCreate(value);

        Assert.NotNull(space);
        Assert.Equal(expected, space.Value.Value);
    }

    [Fact]
    public void CreateReturnsTheNameForValidInput()
    {
        var space = SpaceName.Create("  work ");

        Assert.Equal("work", space.Value);
        Assert.Equal("work", space.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad/name")]
    [InlineData("_reserved")]
    public void CreateThrowsForInvalidNames(string value)
    {
        Assert.Throws<ArgumentException>(() => SpaceName.Create(value));
    }

    [Fact]
    public void DefaultIsTheDefaultSpace()
    {
        Assert.Equal(SpaceName.DefaultValue, SpaceName.Default.Value);
        Assert.True(SpaceName.Default.IsDefault);
    }

    [Theory]
    [InlineData("default", true)]
    [InlineData("DEFAULT", true)]
    [InlineData("Default", true)]
    [InlineData(" default ", true)]
    [InlineData("defaults", false)]
    [InlineData("work", false)]
    public void IsDefaultMatchesTheDefaultNameCaseInsensitively(string value, bool expected)
    {
        var space = SpaceName.TryCreate(value);

        Assert.NotNull(space);
        Assert.Equal(expected, space.Value.IsDefault);
    }

    [Fact]
    public void NamesCompareByValue()
    {
        Assert.Equal(SpaceName.Create("work"), SpaceName.Create("work"));
        Assert.NotEqual(SpaceName.Create("work"), SpaceName.Create("play"));
    }
}
