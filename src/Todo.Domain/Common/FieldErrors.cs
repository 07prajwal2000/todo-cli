namespace Todo.Domain.Common;

public sealed record FieldErrors(IReadOnlyDictionary<string, string[]> ByField)
{
    public bool IsEmpty => ByField.Count == 0;

    public static FieldErrors ForField(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    public static FieldErrors Of(IDictionary<string, List<string>> accumulated) =>
        new(accumulated.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
}

public sealed class FieldErrorsBuilder
{
    private readonly Dictionary<string, List<string>> _fields = [];

    public bool HasErrors => _fields.Count > 0;

    public FieldErrorsBuilder Add(string field, string message)
    {
        if (!_fields.TryGetValue(field, out var messages))
        {
            messages = [];
            _fields[field] = messages;
        }

        messages.Add(message);
        return this;
    }

    public FieldErrorsBuilder AddIf(bool condition, string field, string message) =>
        condition ? Add(field, message) : this;

    public FieldErrors? Build() => HasErrors ? FieldErrors.Of(_fields) : null;
}
