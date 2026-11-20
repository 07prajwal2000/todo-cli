using System.Globalization;

namespace Todo.Domain.Tasks;

public readonly record struct DueDate(DateOnly Value) : IComparable<DueDate>
{
    public const string Format = "yyyy-MM-dd";

    public static bool TryParse(string? value, out DueDate dueDate)
    {
        dueDate = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateOnly.TryParseExact(value.Trim(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        dueDate = new DueDate(parsed);
        return true;
    }

    public static DueDate From(DateOnly value) => new(value);

    public int CompareTo(DueDate other) => Value.CompareTo(other.Value);

    public static bool operator <(DueDate left, DueDate right) => left.Value < right.Value;
    public static bool operator <=(DueDate left, DueDate right) => left.Value <= right.Value;
    public static bool operator >(DueDate left, DueDate right) => left.Value > right.Value;
    public static bool operator >=(DueDate left, DueDate right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString(Format, CultureInfo.InvariantCulture);
}
