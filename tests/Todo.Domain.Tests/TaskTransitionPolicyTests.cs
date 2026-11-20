using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class TaskTransitionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 30, 0, TimeSpan.Zero);

    private static TaskItem TaskWith(TaskState status, DateTimeOffset? completedAt = null) => new()
    {
        Id = new TaskId(1),
        Name = "Task",
        Status = status,
        CreatedAt = Now.AddDays(-3),
        CompletedAt = completedAt,
    };

    [Fact]
    public void EnteringCompletedStampsCompletedAtFromTheSuppliedNow()
    {
        var task = TaskWith(TaskState.Ongoing);

        var transitioned = TaskTransitionPolicy.Apply(task, TaskState.Completed, Now);

        Assert.Equal(TaskState.Completed, transitioned.Status);
        Assert.Equal(Now, transitioned.CompletedAt);
    }

    [Fact]
    public void EnteringCompletedOverwritesAnEarlierCompletionTimestamp()
    {
        var task = TaskWith(TaskState.Pending, Now.AddDays(-10));

        var transitioned = TaskTransitionPolicy.Apply(task, TaskState.Completed, Now);

        Assert.Equal(Now, transitioned.CompletedAt);
    }

    [Theory]
    [InlineData(TaskState.Pending)]
    [InlineData(TaskState.Ongoing)]
    [InlineData(TaskState.Backlog)]
    public void LeavingCompletedClearsCompletedAt(TaskState target)
    {
        var task = TaskWith(TaskState.Completed, Now.AddDays(-1));

        var transitioned = TaskTransitionPolicy.Apply(task, target, Now);

        Assert.Equal(target, transitioned.Status);
        Assert.Null(transitioned.CompletedAt);
    }

    [Fact]
    public void ATransitionBetweenOpenStatesLeavesCompletedAtNull()
    {
        var task = TaskWith(TaskState.Pending);

        var transitioned = TaskTransitionPolicy.Apply(task, TaskState.Ongoing, Now);

        Assert.Equal(TaskState.Ongoing, transitioned.Status);
        Assert.Null(transitioned.CompletedAt);
    }

    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.Pending)]
    [InlineData(TaskState.Ongoing)]
    [InlineData(TaskState.Completed)]
    public void SameStateReturnsTheIdenticalInstance(TaskState state)
    {
        var task = TaskWith(state, state == TaskState.Completed ? Now.AddDays(-1) : null);

        var transitioned = TaskTransitionPolicy.Apply(task, state, Now);

        Assert.Same(task, transitioned);
        Assert.Equal(task, transitioned);
    }

    [Theory]
    [InlineData(TaskState.Pending, TaskState.Completed, true)]
    [InlineData(TaskState.Backlog, TaskState.Completed, false)]
    [InlineData(TaskState.Completed, TaskState.Backlog, false)]
    [InlineData(TaskState.Completed, TaskState.Pending, true)]
    public void IsAllowedDelegatesToTheTransitionTable(TaskState from, TaskState to, bool expected)
    {
        Assert.Equal(expected, TaskTransitionPolicy.IsAllowed(from, to));
    }

    [Fact]
    public void DescribeRejectionNamesBothStates()
    {
        var message = TaskTransitionPolicy.DescribeRejection(TaskState.Backlog, TaskState.Completed);

        Assert.Contains("backlog", message, StringComparison.Ordinal);
        Assert.Contains("completed", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeRejectionListsTheLegalTargets()
    {
        var message = TaskTransitionPolicy.DescribeRejection(TaskState.Completed, TaskState.Backlog);

        Assert.Contains("pending, ongoing", message, StringComparison.Ordinal);
        Assert.EndsWith("pending, ongoing.", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeRejectionIsActionableForEveryIllegalPair()
    {
        foreach (var from in Enum.GetValues<TaskState>())
        {
            foreach (var to in Enum.GetValues<TaskState>())
            {
                if (StatusTransitions.CanTransition(from, to))
                {
                    continue;
                }

                var message = TaskTransitionPolicy.DescribeRejection(from, to);

                Assert.Contains($"from {TaskStates.Canonical(from)}", message, StringComparison.Ordinal);
                Assert.Contains($"to {TaskStates.Canonical(to)}", message, StringComparison.Ordinal);
                Assert.Contains(StatusTransitions.DescribeAllowedFrom(from), message, StringComparison.Ordinal);
            }
        }
    }
}
