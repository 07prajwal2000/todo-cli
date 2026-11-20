using Todo.Application.Common;
using Todo.Application.Tasks;
using Todo.Application.Tests.Fakes;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Application.Tests;

public sealed class TaskServiceTests
{
    private static readonly SpaceName Space = SpaceName.Default;
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTaskRepository _repository = new();
    private readonly FakeClock _clock = new(Now);
    private readonly TaskService _service;

    public TaskServiceTests() => _service = new TaskService(_repository, _clock);

    private static TaskItem Task(
        int id,
        string name = "Task",
        TaskState status = TaskState.Pending,
        string description = "",
        string remarks = "",
        DueDate? dueOn = null,
        DateTimeOffset? createdAt = null) => new()
        {
            Id = new TaskId(id),
            Name = name,
            Status = status,
            Description = description,
            Remarks = remarks,
            DueOn = dueOn,
            CreatedAt = createdAt ?? Now.AddDays(-1),
        };

    private static DueDate Due(int year, int month, int day) => DueDate.From(new DateOnly(year, month, day));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddAsyncRejectsAMissingNameWithANameFieldKey(string? name)
    {
        var result = await _service.AddAsync(
            Space,
            new TaskDraftInput { Name = name },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.NotNull(result.Error!.Fields);
        Assert.True(result.Error!.Fields!.ContainsKey(TaskSchema.NameColumn));
        Assert.Equal(0, _repository.AddCalls);
    }

    [Fact]
    public async Task AddAsyncStampsCreatedAtFromTheClockAndDefaultsTheStatus()
    {
        var result = await _service.AddAsync(
            Space,
            new TaskDraftInput { Name = "  Write tests  " },
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var created = result.Value;
        Assert.Equal(Now, created.CreatedAt);
        Assert.Equal(TaskState.Pending, created.Status);
        Assert.Equal("Write tests", created.Name);
        Assert.True(created.Id.IsAssigned);
        Assert.Equal(1, _repository.AddCalls);
    }

    [Fact]
    public async Task AddAsyncHonoursTheRequestedState()
    {
        var result = await _service.AddAsync(
            Space,
            new TaskDraftInput { Name = "Ship it", State = TaskState.Ongoing },
            CancellationToken.None);

        Assert.Equal(TaskState.Ongoing, result.Value.Status);
        Assert.Equal(TaskState.Ongoing, _repository.LastDraft!.Status);
    }

    [Fact]
    public async Task AddAsyncStoresEmptyTextWhenDescriptionAndRemarksAreNull()
    {
        var result = await _service.AddAsync(
            Space,
            new TaskDraftInput { Name = "Task" },
            CancellationToken.None);

        Assert.Equal(string.Empty, result.Value.Description);
        Assert.Equal(string.Empty, result.Value.Remarks);
        Assert.Equal(string.Empty, _repository.LastDraft!.Description);
        Assert.Equal(string.Empty, _repository.LastDraft!.Remarks);
    }

    [Fact]
    public async Task AddAsyncPassesTheDueDateThrough()
    {
        var result = await _service.AddAsync(
            Space,
            new TaskDraftInput { Name = "Task", DueOn = Due(2026, 12, 1) },
            CancellationToken.None);

        Assert.Equal(Due(2026, 12, 1), result.Value.DueOn);
    }

    [Fact]
    public async Task AddAsyncPropagatesARepositoryFailure()
    {
        _repository.AddFailure = TodoError.Unavailable("Sheet is down.");

        var result = await _service.AddAsync(
            Space,
            new TaskDraftInput { Name = "Task" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
    }

    [Fact]
    public async Task ListAsyncDefaultsToPendingAndOngoingOnly()
    {
        _repository.Seed(
            Space,
            Task(1, "pending", TaskState.Pending),
            Task(2, "ongoing", TaskState.Ongoing),
            Task(3, "backlog", TaskState.Backlog),
            Task(4, "completed", TaskState.Completed));

        var result = await _service.ListAsync(Space, new TaskQuery(), CancellationToken.None);

        Assert.Equal(["ongoing", "pending"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncIncludeAllStatesReturnsEverything()
    {
        _repository.Seed(
            Space,
            Task(1, "pending", TaskState.Pending),
            Task(2, "ongoing", TaskState.Ongoing),
            Task(3, "backlog", TaskState.Backlog),
            Task(4, "completed", TaskState.Completed));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { IncludeAllStates = true },
            CancellationToken.None);

        Assert.Equal(4, result.Value.Count);
        Assert.Equal(
            [TaskState.Ongoing, TaskState.Pending, TaskState.Backlog, TaskState.Completed],
            result.Value.Select(task => task.Status));
    }

    [Fact]
    public async Task ListAsyncExplicitStatesFilterExactly()
    {
        _repository.Seed(
            Space,
            Task(1, "pending", TaskState.Pending),
            Task(2, "ongoing", TaskState.Ongoing),
            Task(3, "backlog", TaskState.Backlog),
            Task(4, "completed", TaskState.Completed));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { States = [TaskState.Completed, TaskState.Backlog] },
            CancellationToken.None);

        Assert.Equal(["backlog", "completed"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncExplicitStatesWinOverIncludeAllStates()
    {
        _repository.Seed(
            Space,
            Task(1, "pending", TaskState.Pending),
            Task(2, "completed", TaskState.Completed));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { States = [TaskState.Completed], IncludeAllStates = true },
            CancellationToken.None);

        Assert.Equal(["completed"], result.Value.Select(task => task.Name));
    }

    [Theory]
    [InlineData("WRITE")]
    [InlineData("write")]
    [InlineData("  Write  ")]
    public async Task ListAsyncSearchTermMatchesTheNameCaseInsensitively(string term)
    {
        _repository.Seed(
            Space,
            Task(1, "Write tests", TaskState.Pending),
            Task(2, "Read docs", TaskState.Pending));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SearchTerm = term },
            CancellationToken.None);

        Assert.Equal(["Write tests"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncSearchTermMatchesTheDescriptionCaseInsensitively()
    {
        _repository.Seed(
            Space,
            Task(1, "First", TaskState.Pending, description: "Contains NEEDLE"),
            Task(2, "Second", TaskState.Pending, description: "nothing here"));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SearchTerm = "needle" },
            CancellationToken.None);

        Assert.Equal(["First"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncSearchTermMatchesTheRemarksCaseInsensitively()
    {
        _repository.Seed(
            Space,
            Task(1, "First", TaskState.Pending, remarks: "blocked on NEEDLE"),
            Task(2, "Second", TaskState.Pending, remarks: "clear"));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SearchTerm = "Needle" },
            CancellationToken.None);

        Assert.Equal(["First"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncAWhitespaceSearchTermIsIgnored()
    {
        _repository.Seed(
            Space,
            Task(1, "First", TaskState.Pending),
            Task(2, "Second", TaskState.Pending));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SearchTerm = "   " },
            CancellationToken.None);

        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task ListAsyncDueBeforeIncludesThatDayAndExcludesUndatedTasks()
    {
        _repository.Seed(
            Space,
            Task(1, "before", TaskState.Pending, dueOn: Due(2026, 4, 30)),
            Task(2, "on", TaskState.Pending, dueOn: Due(2026, 5, 1)),
            Task(3, "after", TaskState.Pending, dueOn: Due(2026, 5, 2)),
            Task(4, "undated", TaskState.Pending));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { DueBefore = Due(2026, 5, 1) },
            CancellationToken.None);

        Assert.Equal(["before", "on"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncOrdersByStateThenDueOnThenId()
    {
        _repository.Seed(
            Space,
            Task(9, "completed", TaskState.Completed),
            Task(8, "backlog", TaskState.Backlog),
            Task(7, "pending-late", TaskState.Pending, dueOn: Due(2026, 6, 1)),
            Task(6, "pending-undated", TaskState.Pending),
            Task(5, "pending-early", TaskState.Pending, dueOn: Due(2026, 5, 1)),
            Task(4, "ongoing-undated", TaskState.Ongoing),
            Task(3, "ongoing-late", TaskState.Ongoing, dueOn: Due(2026, 7, 1)),
            Task(2, "ongoing-early-b", TaskState.Ongoing, dueOn: Due(2026, 5, 1)),
            Task(1, "ongoing-early-a", TaskState.Ongoing, dueOn: Due(2026, 5, 1)));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { IncludeAllStates = true },
            CancellationToken.None);

        Assert.Equal(
            [
                "ongoing-early-a",
                "ongoing-early-b",
                "ongoing-late",
                "ongoing-undated",
                "pending-early",
                "pending-late",
                "pending-undated",
                "backlog",
                "completed",
            ],
            result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncSortByDuePutsUndatedTasksLast()
    {
        _repository.Seed(
            Space,
            Task(1, "undated", TaskState.Pending),
            Task(2, "later", TaskState.Pending, dueOn: Due(2026, 9, 1)),
            Task(3, "earlier", TaskState.Pending, dueOn: Due(2026, 1, 1)));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SortBy = TaskSortField.Due },
            CancellationToken.None);

        Assert.Equal(["earlier", "later", "undated"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncSortByIdOrdersByAscendingId()
    {
        _repository.Seed(
            Space,
            Task(30, "c", TaskState.Ongoing),
            Task(10, "a", TaskState.Completed),
            Task(20, "b", TaskState.Backlog));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SortBy = TaskSortField.Id, IncludeAllStates = true },
            CancellationToken.None);

        Assert.Equal(["a", "b", "c"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncSortByStatusFollowsTheDocumentedPriority()
    {
        _repository.Seed(
            Space,
            Task(1, "completed", TaskState.Completed),
            Task(2, "backlog", TaskState.Backlog),
            Task(3, "pending", TaskState.Pending),
            Task(4, "ongoing", TaskState.Ongoing));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SortBy = TaskSortField.Status, IncludeAllStates = true },
            CancellationToken.None);

        Assert.Equal(
            ["ongoing", "pending", "backlog", "completed"],
            result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncSortByCreatedPutsNewestFirst()
    {
        _repository.Seed(
            Space,
            Task(1, "oldest", TaskState.Pending, createdAt: Now.AddDays(-10)),
            Task(2, "middle", TaskState.Pending, createdAt: Now.AddDays(-5)),
            Task(3, "newest", TaskState.Pending, createdAt: Now));

        var result = await _service.ListAsync(
            Space,
            new TaskQuery { SortBy = TaskSortField.Created },
            CancellationToken.None);

        Assert.Equal(["newest", "middle", "oldest"], result.Value.Select(task => task.Name));
    }

    [Fact]
    public async Task ListAsyncPropagatesARepositoryFailure()
    {
        _repository.ListFailure = TodoError.Unavailable("Sheet is down.");

        var result = await _service.ListAsync(Space, new TaskQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
    }

    [Fact]
    public async Task GetAsyncReturnsAnExistingTask()
    {
        _repository.Seed(Space, Task(7, "found"));

        var result = await _service.GetAsync(Space, new TaskId(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("found", result.Value.Name);
    }

    [Fact]
    public async Task GetAsyncReportsNotFoundForAnUnknownId()
    {
        var result = await _service.GetAsync(Space, new TaskId(404), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.NotFound, result.Error!.Code);
        Assert.Contains("404", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditAsyncPropagatesNotFoundForAnUnknownId()
    {
        var result = await _service.EditAsync(
            Space,
            new TaskId(404),
            new TaskEditInput { Name = "New name" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.NotFound, result.Error!.Code);
        Assert.Equal(0, _repository.UpdateCalls);
    }

    [Fact]
    public async Task EditAsyncChangesOnlyTheSuppliedField()
    {
        _repository.Seed(Space, Task(1, "Old name", TaskState.Ongoing, description: "Keep me", remarks: "And me"));

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput { Name = "  New name  " },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", result.Value.Name);
        Assert.Equal("Keep me", result.Value.Description);
        Assert.Equal("And me", result.Value.Remarks);
        Assert.Equal(TaskState.Ongoing, result.Value.Status);
        Assert.Equal(1, _repository.UpdateCalls);
        Assert.Equal("Keep me", _repository.TasksIn(Space)[0].Description);
    }

    [Fact]
    public async Task EditAsyncRejectsAnInvalidSuppliedName()
    {
        _repository.Seed(Space, Task(1, "Old name"));

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput { Name = "   " },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.True(result.Error!.Fields!.ContainsKey(TaskSchema.NameColumn));
        Assert.Equal(0, _repository.UpdateCalls);
    }

    [Fact]
    public async Task EditAsyncRejectsAnOverlongSuppliedDescription()
    {
        _repository.Seed(Space, Task(1, "Task"));

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput { Description = new string('d', TaskValidator.MaxDescriptionLength + 1) },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.True(result.Error!.Fields!.ContainsKey(TaskSchema.DescriptionColumn));
    }

    [Fact]
    public async Task EditAsyncClearDueNullsTheDueDate()
    {
        _repository.Seed(Space, Task(1, "Task", dueOn: Due(2026, 12, 1)));

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput { ClearDue = true },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.DueOn);
        Assert.Equal(1, _repository.UpdateCalls);
    }

    [Fact]
    public async Task EditAsyncClearDueWinsOverASuppliedDueDate()
    {
        _repository.Seed(Space, Task(1, "Task", dueOn: Due(2026, 12, 1)));

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput { ClearDue = true, DueOn = Due(2027, 1, 1) },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.DueOn);
    }

    [Fact]
    public async Task EditAsyncSetsTheDueDateWhenItIsSuppliedWithoutClearDue()
    {
        _repository.Seed(Space, Task(1, "Task"));

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput { DueOn = Due(2027, 1, 1) },
            CancellationToken.None);

        Assert.Equal(Due(2027, 1, 1), result.Value.DueOn);
    }

    [Fact]
    public async Task EditAsyncWithNoEffectiveChangeSkipsTheRepositoryWrite()
    {
        var original = Task(1, "Task", description: "Same");
        _repository.Seed(Space, original);

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(original, result.Value);
        Assert.Equal(0, _repository.UpdateCalls);
        Assert.Equal(1, _repository.FindCalls);
    }

    [Fact]
    public async Task EditAsyncWhitespaceOnlyRewriteOfTheSameNameIsANoOp()
    {
        _repository.Seed(Space, Task(1, "Task"));

        var result = await _service.EditAsync(
            Space,
            new TaskId(1),
            new TaskEditInput { Name = "  Task  " },
            CancellationToken.None);

        Assert.Equal("Task", result.Value.Name);
        Assert.Equal(0, _repository.UpdateCalls);
    }

    [Fact]
    public async Task ChangeStateAsyncAllowsPendingToOngoing()
    {
        _repository.Seed(Space, Task(1, "Task", TaskState.Pending));

        var result = await _service.ChangeStateAsync(
            Space,
            new TaskId(1),
            TaskState.Ongoing,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskState.Ongoing, result.Value.Status);
        Assert.Null(result.Value.CompletedAt);
        Assert.Equal(1, _repository.UpdateCalls);
    }

    [Fact]
    public async Task ChangeStateAsyncPendingToCompletedStampsCompletedAtFromTheClock()
    {
        _repository.Seed(Space, Task(1, "Task", TaskState.Pending));

        var result = await _service.ChangeStateAsync(
            Space,
            new TaskId(1),
            TaskState.Completed,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskState.Completed, result.Value.Status);
        Assert.Equal(Now, result.Value.CompletedAt);
    }

    [Fact]
    public async Task ChangeStateAsyncCompletedToPendingClearsCompletedAt()
    {
        _repository.Seed(Space, Task(1, "Task", TaskState.Completed));

        var result = await _service.ChangeStateAsync(
            Space,
            new TaskId(1),
            TaskState.Pending,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskState.Pending, result.Value.Status);
        Assert.Null(result.Value.CompletedAt);
        Assert.Null(_repository.TasksIn(Space)[0].CompletedAt);
    }

    [Fact]
    public async Task ChangeStateAsyncRejectsAnIllegalBacklogToCompletedMove()
    {
        _repository.Seed(Space, Task(1, "Task", TaskState.Backlog));

        var result = await _service.ChangeStateAsync(
            Space,
            new TaskId(1),
            TaskState.Completed,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.Contains("backlog", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("completed", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(0, _repository.UpdateCalls);
    }

    [Fact]
    public async Task ChangeStateAsyncReportsNotFoundForAnUnknownId()
    {
        var result = await _service.ChangeStateAsync(
            Space,
            new TaskId(404),
            TaskState.Completed,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.NotFound, result.Error!.Code);
    }

    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.Pending)]
    [InlineData(TaskState.Ongoing)]
    [InlineData(TaskState.Completed)]
    public async Task ChangeStateAsyncWithTheSameStateDoesNotWrite(TaskState state)
    {
        var original = Task(1, "Task", state);
        _repository.Seed(Space, original);

        var result = await _service.ChangeStateAsync(
            Space,
            new TaskId(1),
            state,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(original, result.Value);
        Assert.Equal(0, _repository.UpdateCalls);
    }

    [Fact]
    public async Task DeleteAsyncRemovesTheTask()
    {
        _repository.Seed(Space, Task(1, "Task"), Task(2, "Other"));

        var result = await _service.DeleteAsync(Space, new TaskId(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _repository.DeleteCalls);
        Assert.Equal(["Other"], _repository.TasksIn(Space).Select(task => task.Name));
    }

    [Fact]
    public async Task DeleteAsyncPropagatesNotFound()
    {
        var result = await _service.DeleteAsync(Space, new TaskId(404), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.NotFound, result.Error!.Code);
    }
}
