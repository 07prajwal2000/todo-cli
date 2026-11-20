namespace Todo.Infrastructure.Configuration.Tests;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNowIsCloseToSystemTime()
    {
        var clock = new SystemClock();

        var delta = (DateTimeOffset.UtcNow - clock.UtcNow).Duration();

        Assert.True(delta < TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }

    [Fact]
    public void TodayMatchesUtcDate()
    {
        var clock = new SystemClock();

        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), clock.Today);
    }
}
