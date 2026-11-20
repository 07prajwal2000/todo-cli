using Todo.Application.Common;

namespace Todo.Application.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset? utcNow = null) => UtcNow = utcNow ?? new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; set; }

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
