using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class TaskIdTests
{
    [Fact]
    public void DefaultIdIsUnassigned()
    {
        var id = default(TaskId);

        Assert.Equal(0, id.Value);
        Assert.False(id.IsAssigned);
        Assert.Equal(TaskId.None, id);
    }

    [Fact]
    public void NoneIsTheSameAsAnUnassignedId()
    {
        Assert.Equal(0, TaskId.None.Value);
        Assert.False(TaskId.None.IsAssigned);
        Assert.Equal(default(TaskId), TaskId.None);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(42, true)]
    public void IsAssignedRequiresAPositiveValue(int value, bool expected)
    {
        Assert.Equal(expected, new TaskId(value).IsAssigned);
    }

    [Fact]
    public void ComparisonOperatorsFollowTheNumericValue()
    {
        var low = new TaskId(3);
        var high = new TaskId(7);

        Assert.True(low < high);
        Assert.True(low <= high);
        Assert.True(high > low);
        Assert.True(high >= low);
        Assert.False(high < low);
        Assert.False(low > high);
        Assert.True(low <= new TaskId(3));
        Assert.True(low >= new TaskId(3));
    }

    [Fact]
    public void CompareToOrdersByValue()
    {
        Assert.True(new TaskId(3).CompareTo(new TaskId(7)) < 0);
        Assert.True(new TaskId(7).CompareTo(new TaskId(3)) > 0);
        Assert.Equal(0, new TaskId(7).CompareTo(new TaskId(7)));
    }

    [Fact]
    public void EqualityIsValueBased()
    {
        Assert.Equal(new TaskId(5), new TaskId(5));
        Assert.NotEqual(new TaskId(5), new TaskId(6));
        Assert.True(new TaskId(5) == new TaskId(5));
        Assert.True(new TaskId(5) != new TaskId(6));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(7, "7")]
    [InlineData(-4, "-4")]
    public void ToStringRendersTheRawValue(int value, string expected)
    {
        Assert.Equal(expected, new TaskId(value).ToString());
    }
}
