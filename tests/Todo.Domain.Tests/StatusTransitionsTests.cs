using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class StatusTransitionsTests
{
    [Fact]
    public void BacklogMayMoveToPendingAndOngoingOnly()
    {
        Assert.Equal([TaskState.Pending, TaskState.Ongoing], StatusTransitions.AllowedFrom(TaskState.Backlog));
    }

    [Fact]
    public void PendingMayMoveToOngoingBacklogAndCompletedOnly()
    {
        Assert.Equal(
            [TaskState.Ongoing, TaskState.Backlog, TaskState.Completed],
            StatusTransitions.AllowedFrom(TaskState.Pending));
    }

    [Fact]
    public void OngoingMayMoveToCompletedPendingAndBacklogOnly()
    {
        Assert.Equal(
            [TaskState.Completed, TaskState.Pending, TaskState.Backlog],
            StatusTransitions.AllowedFrom(TaskState.Ongoing));
    }

    [Fact]
    public void CompletedMayMoveToPendingAndOngoingOnly()
    {
        Assert.Equal(
            [TaskState.Pending, TaskState.Ongoing],
            StatusTransitions.AllowedFrom(TaskState.Completed));
    }

    [Theory]
    [InlineData(TaskState.Backlog, TaskState.Pending)]
    [InlineData(TaskState.Backlog, TaskState.Ongoing)]
    [InlineData(TaskState.Pending, TaskState.Ongoing)]
    [InlineData(TaskState.Pending, TaskState.Backlog)]
    [InlineData(TaskState.Pending, TaskState.Completed)]
    [InlineData(TaskState.Ongoing, TaskState.Completed)]
    [InlineData(TaskState.Ongoing, TaskState.Pending)]
    [InlineData(TaskState.Ongoing, TaskState.Backlog)]
    [InlineData(TaskState.Completed, TaskState.Pending)]
    [InlineData(TaskState.Completed, TaskState.Ongoing)]
    public void CanTransitionAllowsTheDocumentedEdges(TaskState from, TaskState to)
    {
        Assert.True(StatusTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(TaskState.Backlog, TaskState.Completed)]
    [InlineData(TaskState.Completed, TaskState.Backlog)]
    public void CanTransitionRejectsBacklogAndCompletedShortcuts(TaskState from, TaskState to)
    {
        Assert.False(StatusTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.Pending)]
    [InlineData(TaskState.Ongoing)]
    [InlineData(TaskState.Completed)]
    public void CanTransitionIsTrueForTheSameState(TaskState state)
    {
        Assert.True(StatusTransitions.CanTransition(state, state));
    }

    [Fact]
    public void DescribeAllowedFromListsOnlyTheLegalTargets()
    {
        Assert.Equal("pending, ongoing", StatusTransitions.DescribeAllowedFrom(TaskState.Backlog));
        Assert.Equal("ongoing, backlog, completed", StatusTransitions.DescribeAllowedFrom(TaskState.Pending));
        Assert.Equal("completed, pending, backlog", StatusTransitions.DescribeAllowedFrom(TaskState.Ongoing));
        Assert.Equal("pending, ongoing", StatusTransitions.DescribeAllowedFrom(TaskState.Completed));
    }

    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.Pending)]
    [InlineData(TaskState.Ongoing)]
    [InlineData(TaskState.Completed)]
    public void DescribeAllowedFromNeverMentionsTheStateItself(TaskState state)
    {
        var allowed = StatusTransitions.AllowedFrom(state);

        Assert.DoesNotContain(state, allowed);
        Assert.DoesNotContain(
            TaskStates.Canonical(state),
            StatusTransitions.DescribeAllowedFrom(state),
            StringComparison.Ordinal);
    }
}
