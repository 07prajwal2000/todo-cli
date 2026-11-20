namespace Todo.Application.Setup;

/// <summary>
/// Google Drive file ids are base64url, so they contain letters, digits, '-' and '_'. Accepting only
/// alphanumerics rejected perfectly valid ids, so the charset is the real one and the check lives
/// here, where it can be tested.
/// </summary>
public static class SpreadsheetId
{
    private const int MinLength = 20;

    private const string UrlMarker = "/spreadsheets/d/";

    public static string? TryExtract(string? input, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "A spreadsheet ID is required.";
            return null;
        }

        var trimmed = input.Trim();

        if (!trimmed.Contains('/'))
        {
            return IsPlausible(trimmed) ? trimmed : Invalid(out error);
        }

        var index = trimmed.IndexOf(UrlMarker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            error = "That URL does not look like a Google Sheets link.";
            return null;
        }

        var remainder = trimmed[(index + UrlMarker.Length)..];
        var end = remainder.IndexOfAny(['/', '?', '#']);
        var candidate = end < 0 ? remainder : remainder[..end];

        return IsPlausible(candidate) ? candidate : Invalid(out error);
    }

    // ASCII only: char.IsLetterOrDigit accepts unicode letters, which would let a pasted error
    // message through instead of the id.
    private static bool IsPlausible(string value) =>
        value.Length >= MinLength && value.All(IsIdCharacter);

    private static bool IsIdCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '-' or '_';

    private static string? Invalid(out string? error)
    {
        error = "That does not look like a spreadsheet ID. It is the long id from the sheet URL and " +
            "uses letters, digits, '-' and '_'.";
        return null;
    }
}
