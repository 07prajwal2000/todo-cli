using Todo.Application.Setup;

namespace Todo.Application.Tests;

public sealed class SpreadsheetIdTests
{
    // A real id: it contains underscores, which the original alphanumeric-only check rejected.
    private const string RealId = "1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA";

    [Fact]
    public void AcceptsAnIdContainingUnderscores()
    {
        var id = SpreadsheetId.TryExtract(RealId, out var error);

        Assert.Equal(RealId, id);
        Assert.Null(error);
    }

    [Fact]
    public void AcceptsAnIdContainingHyphens()
    {
        const string idWithHyphens = "1SD-K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA";

        Assert.Equal(idWithHyphens, SpreadsheetId.TryExtract(idWithHyphens, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void AcceptsTheFullEditUrl()
    {
        var id = SpreadsheetId.TryExtract(
            "https://docs.google.com/spreadsheets/d/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA/edit?gid=0#gid=0",
            out var error);

        Assert.Equal(RealId, id);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA")]
    [InlineData("https://docs.google.com/spreadsheets/d/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA/edit")]
    [InlineData("https://docs.google.com/spreadsheets/d/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA/edit#gid=123456")]
    [InlineData("https://docs.google.com/spreadsheets/d/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA/edit?usp=sharing")]
    [InlineData("  https://docs.google.com/spreadsheets/d/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA/edit  ")]
    [InlineData("HTTPS://DOCS.GOOGLE.COM/SPREADSHEETS/D/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA/EDIT")]
    public void AcceptsEveryCommonUrlShape(string url)
    {
        Assert.Equal(RealId, SpreadsheetId.TryExtract(url, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TrimsSurroundingWhitespaceOnABareId()
    {
        Assert.Equal(RealId, SpreadsheetId.TryExtract($"  {RealId}  ", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RejectsEmptyInput(string? input)
    {
        Assert.Null(SpreadsheetId.TryExtract(input, out var error));
        Assert.Equal("A spreadsheet ID is required.", error);
    }

    [Fact]
    public void RejectsATooShortId()
    {
        Assert.Null(SpreadsheetId.TryExtract("short", out var error));
        Assert.Contains("does not look like a spreadsheet ID", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnIdWithUnsupportedCharacters()
    {
        Assert.Null(SpreadsheetId.TryExtract("1SD K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA", out var error));
        Assert.Contains("does not look like a spreadsheet ID", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNonAsciiLookalikes()
    {
        Assert.Null(SpreadsheetId.TryExtract("1SDК28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA", out _));
    }

    [Fact]
    public void RejectsAUrlFromAnotherGoogleProduct()
    {
        Assert.Null(SpreadsheetId.TryExtract("https://docs.google.com/document/d/1SD_K28Ox5dxuuzU78jGLz08dkH73ghHLeNiWnxknVhA/edit", out var error));
        Assert.Equal("That URL does not look like a Google Sheets link.", error);
    }

    [Fact]
    public void RejectsAUrlWithNoIdAfterTheMarker()
    {
        Assert.Null(SpreadsheetId.TryExtract("https://docs.google.com/spreadsheets/d//edit", out var error));
        Assert.Contains("does not look like a spreadsheet ID", error, StringComparison.Ordinal);
    }
}
