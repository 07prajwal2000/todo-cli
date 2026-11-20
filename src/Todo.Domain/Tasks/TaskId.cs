namespace Todo.Domain.Tasks;

public readonly record struct TaskId(int Value) : IComparable<TaskId>
{
    public static readonly TaskId None = new(0);

    public bool IsAssigned => Value > 0;

    public int CompareTo(TaskId other) => Value.CompareTo(other.Value);

    public static bool operator <(TaskId left, TaskId right) => left.Value < right.Value;
    public static bool operator <=(TaskId left, TaskId right) => left.Value <= right.Value;
    public static bool operator >(TaskId left, TaskId right) => left.Value > right.Value;
    public static bool operator >=(TaskId left, TaskId right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString();
}
