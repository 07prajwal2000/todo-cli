namespace Todo.Domain.Spaces;

public readonly record struct SpaceName
{
    public const string DefaultValue = "default";
    public const int MaxLength = 64;

    private SpaceName(string value) => Value = value;

    public string Value { get; }

    public static SpaceName Default => new(DefaultValue);

    public bool IsDefault => string.Equals(Value, DefaultValue, StringComparison.OrdinalIgnoreCase);

    public static bool IsValid(string? value, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Space name is required.";
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            error = $"Space name must be at most {MaxLength} characters.";
            return false;
        }

        // Sheet tab names may not contain these characters, and a leading underscore is
        // reserved for internal tabs such as _meta.
        if (trimmed.IndexOfAny(['/', '\\', '?', '*', '[', ']', ':']) >= 0)
        {
            error = "Space name may not contain any of: / \\ ? * [ ] :";
            return false;
        }

        if (trimmed[0] == '_')
        {
            error = "Space name may not start with an underscore.";
            return false;
        }

        if (trimmed[0] == '\'' || trimmed[^1] == '\'')
        {
            error = "Space name may not start or end with an apostrophe.";
            return false;
        }

        return true;
    }

    public static SpaceName? TryCreate(string? value) =>
        IsValid(value, out _) ? new SpaceName(value!.Trim()) : null;

    public static SpaceName Create(string value) =>
        TryCreate(value) ?? throw new ArgumentException($"Invalid space name: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
