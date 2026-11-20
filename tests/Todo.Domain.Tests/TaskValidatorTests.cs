using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class TaskValidatorTests
{
    private const string NameKey = "name";
    private const string DescriptionKey = "description";
    private const string RemarksKey = "remarks";

    private static string Repeat(char character, int count) => new(character, count);

    [Fact]
    public void AValidTaskPassesCreateValidation()
    {
        var errors = TaskValidator.ValidateForCreate(
            "Write tests",
            "Some description",
            "Some remarks",
            DueDate.From(new DateOnly(2026, 12, 1)));

        Assert.Null(errors);
    }

    [Fact]
    public void MinimalValidTaskPassesCreateValidation()
    {
        Assert.Null(TaskValidator.ValidateForCreate("Write tests", null, null, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void AMissingNameFailsCreateValidationKeyedOnTheNameColumn(string? name)
    {
        var errors = TaskValidator.ValidateForCreate(name, null, null, null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(NameKey));
        Assert.Contains(errors.ByField[NameKey], message => message.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheNameLengthBoundaryIsInclusive()
    {
        var atLimit = Repeat('a', TaskValidator.MaxNameLength);
        var overLimit = Repeat('a', TaskValidator.MaxNameLength + 1);

        Assert.Null(TaskValidator.ValidateForCreate(atLimit, null, null, null));

        var errors = TaskValidator.ValidateForCreate(overLimit, null, null, null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(NameKey));
        Assert.Single(errors.ByField[NameKey]);
    }

    [Fact]
    public void TrailingWhitespaceDoesNotPushTheNameOverTheLimit()
    {
        var name = Repeat('a', TaskValidator.MaxNameLength) + "   ";

        Assert.Null(TaskValidator.ValidateForCreate(name, null, null, null));
    }

    [Fact]
    public void AnOverlongDescriptionFailsKeyedOnTheDescriptionColumn()
    {
        var errors = TaskValidator.ValidateForCreate("Name", Repeat('d', TaskValidator.MaxDescriptionLength + 1), null, null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(DescriptionKey));
        Assert.False(errors.ByField.ContainsKey(NameKey));
    }

    [Fact]
    public void ADescriptionAtTheLimitPasses()
    {
        var description = Repeat('d', TaskValidator.MaxDescriptionLength);

        Assert.Null(TaskValidator.ValidateForCreate("Name", description, null, null));
    }

    [Fact]
    public void AnOverlongRemarksFailsKeyedOnTheRemarksColumn()
    {
        var errors = TaskValidator.ValidateForCreate("Name", null, Repeat('r', TaskValidator.MaxRemarksLength + 1), null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(RemarksKey));
        Assert.False(errors.ByField.ContainsKey(NameKey));
    }

    [Fact]
    public void ARemarksAtTheLimitPasses()
    {
        var remarks = Repeat('r', TaskValidator.MaxRemarksLength);

        Assert.Null(TaskValidator.ValidateForCreate("Name", null, remarks, null));
    }

    [Fact]
    public void MultipleProblemsAccumulateUnderTheirOwnKeys()
    {
        var errors = TaskValidator.ValidateForCreate(
            "  ",
            Repeat('d', TaskValidator.MaxDescriptionLength + 1),
            Repeat('r', TaskValidator.MaxRemarksLength + 1),
            null);

        Assert.NotNull(errors);
        Assert.Equal(3, errors.ByField.Count);
        Assert.True(errors.ByField.ContainsKey(NameKey));
        Assert.True(errors.ByField.ContainsKey(DescriptionKey));
        Assert.True(errors.ByField.ContainsKey(RemarksKey));
        Assert.All(errors.ByField.Values, messages => Assert.NotEmpty(messages));
    }

    [Fact]
    public void ADueDateBefore1900FailsKeyedOnTheDueOnColumn()
    {
        var errors = TaskValidator.ValidateForCreate(
            "Name",
            null,
            null,
            DueDate.From(new DateOnly(1899, 12, 31)));

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey("due_on"));
    }

    [Fact]
    public void ADueDateIn1900Passes()
    {
        Assert.Null(TaskValidator.ValidateForCreate("Name", null, null, DueDate.From(new DateOnly(1900, 1, 1))));
    }

    [Fact]
    public void APartialUpdateWithNoSuppliedFieldsPasses()
    {
        Assert.Null(TaskValidator.ValidateForUpdate(null, null, null, null));
    }

    [Fact]
    public void APartialUpdateIgnoresNullFieldsEvenIfTheStoredOnesWouldBeInvalid()
    {
        Assert.Null(TaskValidator.ValidateForUpdate("A valid name", null, null, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void APartialUpdateRejectsASuppliedBlankName(string name)
    {
        var errors = TaskValidator.ValidateForUpdate(name, null, null, null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(NameKey));
    }

    [Fact]
    public void APartialUpdateRejectsAnOverlongSuppliedName()
    {
        var errors = TaskValidator.ValidateForUpdate(Repeat('a', TaskValidator.MaxNameLength + 1), null, null, null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(NameKey));
    }

    [Fact]
    public void APartialUpdateRejectsAnOverlongSuppliedDescription()
    {
        var errors = TaskValidator.ValidateForUpdate(null, Repeat('d', TaskValidator.MaxDescriptionLength + 1), null, null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(DescriptionKey));
        Assert.False(errors.ByField.ContainsKey(NameKey));
    }

    [Fact]
    public void APartialUpdateRejectsAnOverlongSuppliedRemarks()
    {
        var errors = TaskValidator.ValidateForUpdate(null, null, Repeat('r', TaskValidator.MaxRemarksLength + 1), null);

        Assert.NotNull(errors);
        Assert.True(errors.ByField.ContainsKey(RemarksKey));
    }

    [Fact]
    public void APartialUpdateAcceptsValidSuppliedFields()
    {
        var errors = TaskValidator.ValidateForUpdate(
            "New name",
            "New description",
            "New remarks",
            DueDate.From(new DateOnly(2026, 1, 1)));

        Assert.Null(errors);
    }
}
