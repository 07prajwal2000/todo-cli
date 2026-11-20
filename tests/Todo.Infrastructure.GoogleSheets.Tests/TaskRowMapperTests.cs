using Todo.Application.Common;
using Todo.Domain.Tasks;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets.Tests;

public sealed class TaskRowMapperTests
{
    private static TaskItem SampleTask() => new()
    {
        Id = new TaskId(7),
        Name = "Write the adapter",
        Description = "Map cells both ways",
        Status = TaskState.Ongoing,
        DueOn = DueDate.TryParse("2026-02-14", out var due) ? due : null,
        CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        CompletedAt = null,
        Remarks = "keep it small",
    };

    [Fact]
    public void ToRow_emits_cells_in_schema_order()
    {
        var row = TaskRowMapper.ToRow(SampleTask());

        Assert.Equal(TaskSchema.Columns.Count, row.Count);
        Assert.Equal(
            ["7", "Write the adapter", "Map cells both ways", "ongoing", "2026-02-14", "2026-01-02T03:04:05Z", "", "keep it small"],
            row.Select(cell => cell.ToString()));
    }

    [Fact]
    public void ToRow_uses_invariant_id_and_utc_timestamps()
    {
        var task = SampleTask() with
        {
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)),
            CompletedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
        };

        var row = TaskRowMapper.ToRow(task);

        Assert.Equal("7", row[0]);
        Assert.Equal("2026-01-02T01:04:05Z", row[5]);
        Assert.Equal("2026-03-04T05:06:07Z", row[6]);
    }

    [Fact]
    public void ToRow_writes_a_blank_due_on_when_absent()
    {
        var row = TaskRowMapper.ToRow(SampleTask() with { DueOn = null });

        Assert.Equal(string.Empty, row[4]);
    }

    [Fact]
    public void FromRow_round_trips_a_task()
    {
        var task = SampleTask();

        var parsed = TaskRowMapper.FromRow(TaskRowMapper.ToRow(task).Cast<object?>().ToList(), 2);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(task.Id, parsed.Value.Id);
        Assert.Equal(task.Name, parsed.Value.Name);
        Assert.Equal(task.Description, parsed.Value.Description);
        Assert.Equal(task.Status, parsed.Value.Status);
        Assert.Equal(task.DueOn, parsed.Value.DueOn);
        Assert.Equal(task.CreatedAt, parsed.Value.CreatedAt);
        Assert.Null(parsed.Value.CompletedAt);
        Assert.Equal(task.Remarks, parsed.Value.Remarks);
    }

    [Fact]
    public void FromRow_round_trips_a_completed_task()
    {
        var completed = SampleTask() with
        {
            Status = TaskState.Completed,
            CompletedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero),
        };

        var parsed = TaskRowMapper.FromRow(TaskRowMapper.ToRow(completed).Cast<object?>().ToList(), 4);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(TaskState.Completed, parsed.Value.Status);
        Assert.Equal(completed.CompletedAt, parsed.Value.CompletedAt);
    }

    [Theory]
    [InlineData(7d)]
    [InlineData("7")]
    [InlineData(" 7 ")]
    public void FromRow_accepts_numbers_or_strings_for_id(object idCell)
    {
        var row = TaskRowMapper.ToRow(SampleTask()).Cast<object?>().ToList();
        row[0] = idCell;

        var parsed = TaskRowMapper.FromRow(row, 2);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(new TaskId(7), parsed.Value.Id);
    }

    [Fact]
    public void FromRow_treats_a_short_row_as_trailing_empty_cells()
    {
        var binding = TaskSchemaBinding.ValidateHeaders(["id", "name", "status", "created_at"]);
        Assert.True(binding.IsSuccess);

        IReadOnlyList<object?> row = ["3", "Short row", "pending", "2026-01-02T03:04:05Z"];

        var parsed = TaskRowMapper.FromRow(row, binding.Value, 5);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(new TaskId(3), parsed.Value.Id);
        Assert.Equal("Short row", parsed.Value.Name);
        Assert.Equal(string.Empty, parsed.Value.Description);
        Assert.Null(parsed.Value.DueOn);
        Assert.Null(parsed.Value.CompletedAt);
        Assert.Equal(string.Empty, parsed.Value.Remarks);
    }

    [Fact]
    public void FromRow_returns_not_found_for_a_blank_id()
    {
        IReadOnlyList<object?> row = ["", "No id", "pending", "2026-01-02T03:04:05Z"];

        var parsed = TaskRowMapper.FromRow(row, 9);

        Assert.True(parsed.IsFailure);
        Assert.Equal(ErrorCode.NotFound, parsed.Error!.Code);
    }

    [Fact]
    public void FromRow_returns_conflict_for_a_non_numeric_id()
    {
        IReadOnlyList<object?> row = ["abc", "Bad id", "pending", "2026-01-02T03:04:05Z"];

        var parsed = TaskRowMapper.FromRow(row, 9);

        Assert.True(parsed.IsFailure);
        Assert.Equal(ErrorCode.Conflict, parsed.Error!.Code);
        Assert.Contains("Row 9", parsed.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromRow_returns_conflict_naming_the_row_for_a_bad_status()
    {
        var row = TaskRowMapper.ToRow(SampleTask()).Cast<object?>().ToList();
        row[3] = "sideways";

        var parsed = TaskRowMapper.FromRow(row, 12);

        Assert.True(parsed.IsFailure);
        Assert.Equal(ErrorCode.Conflict, parsed.Error!.Code);
        Assert.Contains("Row 12", parsed.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("sideways", parsed.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromRow_trims_trailing_whitespace_on_status()
    {
        var row = TaskRowMapper.ToRow(SampleTask()).Cast<object?>().ToList();
        row[3] = "  done  ";

        var parsed = TaskRowMapper.FromRow(row, 2);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(TaskState.Completed, parsed.Value.Status);
    }

    [Fact]
    public void FromRow_returns_conflict_naming_the_row_and_value_for_a_bad_created_at()
    {
        var row = TaskRowMapper.ToRow(SampleTask()).Cast<object?>().ToList();
        row[5] = "not-a-timestamp";

        var parsed = TaskRowMapper.FromRow(row, 21);

        Assert.True(parsed.IsFailure);
        Assert.Equal(ErrorCode.Conflict, parsed.Error!.Code);
        Assert.Contains("Row 21", parsed.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("not-a-timestamp", parsed.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromRow_returns_conflict_for_a_bad_due_on()
    {
        var row = TaskRowMapper.ToRow(SampleTask()).Cast<object?>().ToList();
        row[4] = "14/02/2026";

        var parsed = TaskRowMapper.FromRow(row, 3);

        Assert.True(parsed.IsFailure);
        Assert.Equal(ErrorCode.Conflict, parsed.Error!.Code);
        Assert.Contains("14/02/2026", parsed.Error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromRow_treats_a_blank_due_on_as_null(object? dueCell)
    {
        var row = TaskRowMapper.ToRow(SampleTask()).Cast<object?>().ToList();
        row[4] = dueCell;

        var parsed = TaskRowMapper.FromRow(row, 2);

        Assert.True(parsed.IsSuccess);
        Assert.Null(parsed.Value.DueOn);
    }

    [Fact]
    public void FromRow_uses_the_binding_to_honour_reordered_columns()
    {
        var shuffled = TaskSchema.Columns.Reverse().Cast<object?>().ToList();
        var binding = TaskSchemaBinding.ValidateHeaders(shuffled);
        Assert.True(binding.IsSuccess);

        var task = SampleTask();
        var row = TaskRowMapper.ToRow(task).Cast<object?>().Reverse().ToList();

        var parsed = TaskRowMapper.FromRow(row, binding.Value, 2);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(task.Id, parsed.Value.Id);
        Assert.Equal(task.Name, parsed.Value.Name);
        Assert.Equal(task.Status, parsed.Value.Status);
        Assert.Equal(task.DueOn, parsed.Value.DueOn);
        Assert.Equal(task.CreatedAt, parsed.Value.CreatedAt);
    }

    [Fact]
    public void IsBlankRow_detects_padding_rows()
    {
        Assert.True(TaskRowMapper.IsBlankRow([]));
        Assert.True(TaskRowMapper.IsBlankRow([null, string.Empty, "  "]));
        Assert.False(TaskRowMapper.IsBlankRow(["", "name"]));
        Assert.False(TaskRowMapper.IsBlankRow([1d]));
    }

    [Fact]
    public void HeaderRow_matches_the_schema_columns()
    {
        Assert.Equal(TaskSchema.Columns, TaskRowMapper.HeaderRow().Select(cell => (string)cell));
    }
}
