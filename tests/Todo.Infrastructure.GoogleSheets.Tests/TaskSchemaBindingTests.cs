using Todo.Application.Common;
using Todo.Domain.Tasks;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets.Tests;

public sealed class TaskSchemaBindingTests
{
    private const string MissingNameMessage =
        "The sheet is missing required column 'name'. Run 'todo doctor --fix'.";

    [Fact]
    public void ValidateHeaders_accepts_the_canonical_header_row()
    {
        var binding = TaskSchemaBinding.ValidateHeaders(TaskSchema.Columns.Cast<object?>().ToList());

        Assert.True(binding.IsSuccess);
        Assert.Equal(TaskSchema.Columns.Count, binding.Value.Columns.Count);
    }

    [Fact]
    public void ValidateHeaders_reports_a_missing_required_column_with_an_actionable_message()
    {
        IReadOnlyList<object?> headers = ["id", "status", "created_at"];

        var binding = TaskSchemaBinding.ValidateHeaders(headers);

        Assert.True(binding.IsFailure);
        Assert.Equal(ErrorCode.Conflict, binding.Error!.Code);
        Assert.Equal(MissingNameMessage, binding.Error!.Message);
    }

    [Fact]
    public void ValidateHeaders_matches_headers_case_insensitively_and_trimmed()
    {
        IReadOnlyList<object?> headers = [" ID ", "Name", "  Status", "CREATED_AT"];

        var binding = TaskSchemaBinding.ValidateHeaders(headers);

        Assert.True(binding.IsSuccess);
        Assert.Equal(1, binding.Value.IndexOf(TaskSchema.NameColumn));
        Assert.Equal(3, binding.Value.IndexOf(TaskSchema.CreatedAtColumn));
    }

    [Fact]
    public void ValidateHeaders_builds_indexes_for_reordered_columns()
    {
        IReadOnlyList<object?> headers = ["remarks", "completed_at", "due_on", "status", "name", "id", "created_at", "description"];

        var binding = TaskSchemaBinding.ValidateHeaders(headers);

        Assert.True(binding.IsSuccess);
        Assert.Equal(5, binding.Value.IndexOf(TaskSchema.IdColumn));
        Assert.Equal(4, binding.Value.IndexOf(TaskSchema.NameColumn));
        Assert.Equal(7, binding.Value.IndexOf(TaskSchema.DescriptionColumn));
        Assert.Equal(3, binding.Value.IndexOf(TaskSchema.StatusColumn));
        Assert.Equal(2, binding.Value.IndexOf(TaskSchema.DueOnColumn));
        Assert.Equal(6, binding.Value.IndexOf(TaskSchema.CreatedAtColumn));
        Assert.Equal(1, binding.Value.IndexOf(TaskSchema.CompletedAtColumn));
        Assert.Equal(0, binding.Value.IndexOf(TaskSchema.RemarksColumn));
    }

    [Fact]
    public void ValidateHeaders_tolerates_a_header_row_with_extra_columns()
    {
        IReadOnlyList<object?> headers = [.. TaskSchema.Columns.Cast<object?>(), "priority"];

        var binding = TaskSchemaBinding.ValidateHeaders(headers);

        Assert.True(binding.IsSuccess);
        Assert.Equal(TaskSchema.Columns.Count + 1, binding.Value.HeaderCells.Count);
    }

    [Fact]
    public void ValidateHeaders_ignores_blank_cells()
    {
        IReadOnlyList<object?> headers = ["id", null, "name", "  ", "status", "created_at"];

        var binding = TaskSchemaBinding.ValidateHeaders(headers);

        Assert.True(binding.IsSuccess);
        Assert.Equal(5, binding.Value.IndexOf(TaskSchema.CreatedAtColumn));
    }

    [Fact]
    public void ValidateHeaders_reports_the_first_missing_required_column()
    {
        IReadOnlyList<object?> headers = ["name", "status"];

        var binding = TaskSchemaBinding.ValidateHeaders(headers);

        Assert.True(binding.IsFailure);
        Assert.Contains("'id'", binding.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValueAt_returns_null_past_the_end_of_a_short_row()
    {
        var binding = TaskSchemaBinding.Positional;
        IReadOnlyList<object?> row = ["1", "Only two cells"];

        Assert.Equal("1", binding.ValueAt(row, TaskSchema.IdColumn));
        Assert.Null(binding.ValueAt(row, TaskSchema.RemarksColumn));
        Assert.Null(binding.TextAt(row, TaskSchema.RemarksColumn));
    }

    [Fact]
    public void Positional_binding_maps_columns_in_schema_order()
    {
        var binding = TaskSchemaBinding.Positional;

        for (var index = 0; index < TaskSchema.Columns.Count; index++)
        {
            Assert.Equal(index, binding.IndexOf(TaskSchema.Columns[index]));
        }
    }

    [Fact]
    public void FromHeaders_falls_back_to_positional_when_validation_fails()
    {
        IReadOnlyList<object?> headers = ["nonsense"];

        var binding = TaskSchemaBinding.FromHeaders(headers);

        Assert.Equal(0, binding.IndexOf(TaskSchema.IdColumn));
    }
}
