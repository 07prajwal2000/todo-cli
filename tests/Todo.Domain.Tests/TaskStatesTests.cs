using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class TaskStatesTests
{
    [Theory]
    [InlineData("backlog", TaskState.Backlog)]
    [InlineData("pending", TaskState.Pending)]
    [InlineData("ongoing", TaskState.Ongoing)]
    [InlineData("completed", TaskState.Completed)]
    public void TryParseAcceptsEveryCanonicalName(string value, TaskState expected)
    {
        Assert.True(TaskStates.TryParse(value, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("done", TaskState.Completed)]
    [InlineData("complete", TaskState.Completed)]
    [InlineData("closed", TaskState.Completed)]
    [InlineData("wip", TaskState.Ongoing)]
    [InlineData("doing", TaskState.Ongoing)]
    [InlineData("started", TaskState.Ongoing)]
    [InlineData("open", TaskState.Pending)]
    [InlineData("todo", TaskState.Pending)]
    [InlineData("icebox", TaskState.Backlog)]
    [InlineData("later", TaskState.Backlog)]
    public void TryParseAcceptsEveryAlias(string value, TaskState expected)
    {
        Assert.True(TaskStates.TryParse(value, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("BACKLOG", TaskState.Backlog)]
    [InlineData("Pending", TaskState.Pending)]
    [InlineData("OnGoInG", TaskState.Ongoing)]
    [InlineData("DONE", TaskState.Completed)]
    [InlineData("IceBox", TaskState.Backlog)]
    public void TryParseIgnoresCase(string value, TaskState expected)
    {
        Assert.True(TaskStates.TryParse(value, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("  ongoing  ", TaskState.Ongoing)]
    [InlineData("\tcompleted\n", TaskState.Completed)]
    [InlineData(" wip ", TaskState.Ongoing)]
    public void TryParseTrimsSurroundingWhitespace(string value, TaskState expected)
    {
        Assert.True(TaskStates.TryParse(value, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("pend")]
    [InlineData("status")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParseRejectsUnknownOrBlankValues(string? value)
    {
        Assert.False(TaskStates.TryParse(value, out var state));
        Assert.Equal(default, state);
    }

    [Fact]
    public void TryParseDoesNotAcceptNumericEnumValues()
    {
        Assert.False(TaskStates.TryParse("3", out _));
    }

    [Theory]
    [InlineData(TaskState.Backlog, "backlog")]
    [InlineData(TaskState.Pending, "pending")]
    [InlineData(TaskState.Ongoing, "ongoing")]
    [InlineData(TaskState.Completed, "completed")]
    public void CanonicalRoundTripsThroughTryParse(TaskState state, string expected)
    {
        var name = TaskStates.Canonical(state);

        Assert.Equal(expected, name);
        Assert.True(TaskStates.TryParse(name, out var parsed));
        Assert.Equal(state, parsed);
    }

    [Fact]
    public void DescribeAcceptedListsAllFourCanonicalStates()
    {
        var described = TaskStates.DescribeAccepted();

        Assert.Equal("pending, ongoing, backlog, completed", described);

        foreach (var state in Enum.GetValues<TaskState>())
        {
            Assert.Contains(TaskStates.Canonical(state), described, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AllContainsEveryStateExactlyOnce()
    {
        Assert.Equal(Enum.GetValues<TaskState>().Length, TaskStates.All.Count);
        Assert.Equal(Enum.GetValues<TaskState>().OrderBy(state => state), TaskStates.All.OrderBy(state => state));
    }
}
