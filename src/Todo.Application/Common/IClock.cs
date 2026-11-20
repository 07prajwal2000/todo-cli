namespace Todo.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }
}
