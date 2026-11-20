using Todo.Domain.Common;

namespace Todo.Domain.Tests;

public sealed class FieldErrorsTests
{
    [Fact]
    public void ForFieldCreatesASingleEntryDictionary()
    {
        var errors = FieldErrors.ForField("name", "Name is required.");

        Assert.False(errors.IsEmpty);
        Assert.Single(errors.ByField);
        Assert.Equal(["Name is required."], errors.ByField["name"]);
    }

    [Fact]
    public void OfSnapshotsTheAccumulatedMessages()
    {
        var accumulated = new Dictionary<string, List<string>>
        {
            ["name"] = ["first", "second"],
        };

        var errors = FieldErrors.Of(accumulated);
        accumulated["name"].Add("third");

        Assert.Equal(["first", "second"], errors.ByField["name"]);
    }

    [Fact]
    public void BuilderAccumulatesMultipleMessagesPerField()
    {
        var errors = new FieldErrorsBuilder()
            .Add("name", "first")
            .Add("name", "second")
            .Build();

        Assert.NotNull(errors);
        Assert.Equal(["first", "second"], errors.ByField["name"]);
    }

    [Fact]
    public void BuilderKeepsFieldsApart()
    {
        var errors = new FieldErrorsBuilder()
            .Add("name", "name problem")
            .Add("description", "description problem")
            .Build();

        Assert.NotNull(errors);
        Assert.Equal(2, errors.ByField.Count);
        Assert.Equal(["name problem"], errors.ByField["name"]);
        Assert.Equal(["description problem"], errors.ByField["description"]);
    }

    [Fact]
    public void BuilderReturnsNullWhenNothingWasAdded()
    {
        Assert.Null(new FieldErrorsBuilder().Build());
    }

    [Fact]
    public void HasErrorsReflectsWhetherAnythingWasAdded()
    {
        var builder = new FieldErrorsBuilder();

        Assert.False(builder.HasErrors);

        builder.Add("name", "problem");

        Assert.True(builder.HasErrors);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddIfOnlyAddsWhenTheConditionHolds(bool condition)
    {
        var errors = new FieldErrorsBuilder()
            .AddIf(condition, "name", "problem")
            .Build();

        if (condition)
        {
            Assert.NotNull(errors);
            Assert.Equal(["problem"], errors.ByField["name"]);
        }
        else
        {
            Assert.Null(errors);
        }
    }

    [Fact]
    public void BuilderReturnsTheSameInstanceForChaining()
    {
        var builder = new FieldErrorsBuilder();

        Assert.Same(builder, builder.Add("name", "problem"));
        Assert.Same(builder, builder.AddIf(true, "name", "another"));
    }
}
