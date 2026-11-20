using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class TaskItemTests
{
    [Fact]
    public void NewTaskDefaultsToPendingWithEmptyOptionalText()
    {
        var task = new TaskItem { Id = new TaskId(1), Name = "Task" };

        Assert.Equal(TaskState.Pending, task.Status);
        Assert.Equal(string.Empty, task.Description);
        Assert.Equal(string.Empty, task.Remarks);
        Assert.Null(task.DueOn);
        Assert.Null(task.CompletedAt);
        Assert.True(task.IsOpen);
    }

    [Theory]
    [InlineData(TaskState.Backlog, true)]
    [InlineData(TaskState.Pending, true)]
    [InlineData(TaskState.Ongoing, true)]
    [InlineData(TaskState.Completed, false)]
    public void IsOpenIsTrueForEveryStateButCompleted(TaskState status, bool expected)
    {
        var task = new TaskItem { Id = new TaskId(1), Name = "Task", Status = status };

        Assert.Equal(expected, task.IsOpen);
    }

    [Fact]
    public void RecordsCompareByValue()
    {
        var a = new TaskItem { Id = new TaskId(1), Name = "Task" };
        var b = new TaskItem { Id = new TaskId(1), Name = "Task" };

        Assert.Equal(a, b);
        Assert.NotSame(a, b);
    }
}
