using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Todo.Application.Common;
using Todo.Application.Tasks;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Task storage on a per-space tab. Row numbers are always resolved from the sheet at call time and
/// never persisted, because a user inserting or deleting rows by hand would invalidate any cache.
/// </summary>
public sealed class SheetsTaskRepository : ITaskRepository
{
    private readonly SheetsGateway _gateway;
    private readonly ILogger<SheetsTaskRepository> _logger;

    public SheetsTaskRepository(SheetsGateway gateway, ILogger<SheetsTaskRepository>? logger = null)
    {
        _gateway = gateway;
        _logger = logger ?? NullLogger<SheetsTaskRepository>.Instance;
    }

    public async Task<Result<IReadOnlyList<TaskItem>>> ListAsync(
        SpaceName space,
        CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(space, cancellationToken).ConfigureAwait(false);
        if (!opened.TryGetValue(out var tab))
        {
            return Result<IReadOnlyList<TaskItem>>.Failure(opened.Error!);
        }

        var mapped = MapRows(tab);
        return mapped.IsSuccess
            ? Result<IReadOnlyList<TaskItem>>.Success(mapped.Value.Tasks)
            : Result<IReadOnlyList<TaskItem>>.Failure(mapped.Error!);
    }

    public async Task<Result<TaskItem>> FindAsync(
        SpaceName space,
        TaskId id,
        CancellationToken cancellationToken)
    {
        var located = await LocateAsync(space, id, cancellationToken).ConfigureAwait(false);
        return located.IsSuccess
            ? Result<TaskItem>.Success(located.Value.Task)
            : Result<TaskItem>.Failure(located.Error!);
    }

    public async Task<Result<TaskItem>> AddAsync(
        SpaceName space,
        TaskDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var opened = await OpenAsync(space, cancellationToken).ConfigureAwait(false);
        if (!opened.TryGetValue(out var tab))
        {
            return Result<TaskItem>.Failure(opened.Error!);
        }

        var mapped = MapRows(tab);
        if (!mapped.TryGetValue(out var rows))
        {
            return Result<TaskItem>.Failure(mapped.Error!);
        }

        var meta = await _gateway.ReadMetaAsync(cancellationToken).ConfigureAwait(false);
        if (!meta.TryGetValue(out var metaTable))
        {
            return Result<TaskItem>.Failure(meta.Error!);
        }

        var nextId = ResolveNextId(space, rows.Tasks, metaTable);
        var task = new TaskItem
        {
            Id = new TaskId(nextId),
            Name = draft.Name,
            Description = draft.Description,
            Status = draft.Status,
            DueOn = draft.DueOn,
            CreatedAt = draft.CreatedAt,
            Remarks = draft.Remarks,
        };

        var appended = await _gateway
            .AppendValuesAsync(tab.Layout.TailRange, TaskRowMapper.ToRow(task), cancellationToken)
            .ConfigureAwait(false);

        if (!appended.IsSuccess)
        {
            return Result<TaskItem>.Failure(appended.Error!);
        }

        await PersistNextIdAsync(space, nextId, metaTable, cancellationToken).ConfigureAwait(false);

        return Result<TaskItem>.Success(task);
    }

    public async Task<Result<TaskItem>> UpdateAsync(
        SpaceName space,
        TaskItem task,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        var located = await LocateAsync(space, task.Id, cancellationToken).ConfigureAwait(false);
        if (!located.TryGetValue(out var position))
        {
            return Result<TaskItem>.Failure(located.Error!);
        }

        var range = position.Tab.Layout.RowRange(TaskTab.RowNumber(position.DataRowIndex));

        var written = await _gateway
            .WriteValuesAsync(range, [TaskRowMapper.ToRow(task)], cancellationToken)
            .ConfigureAwait(false);

        return written.IsSuccess
            ? Result<TaskItem>.Success(task)
            : Result<TaskItem>.Failure(written.Error!);
    }

    public async Task<Result> DeleteAsync(SpaceName space, TaskId id, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(space, id, cancellationToken).ConfigureAwait(false);
        if (!located.TryGetValue(out var position))
        {
            return Result.Failure(located.Error!);
        }

        return await _gateway
            .DeleteRowsAsync(
                position.Tab.Sheet.SheetId,
                TaskTab.RowNumber(position.DataRowIndex),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<TaskTab>> OpenAsync(SpaceName space, CancellationToken cancellationToken)
    {
        if (!SpaceName.IsValid(space.Value, out var error))
        {
            return Result<TaskTab>.Failure(TodoError.Validation(error!));
        }

        return await _gateway.OpenTaskTabAsync(space.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LocatedTask>> LocateAsync(
        SpaceName space,
        TaskId id,
        CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(space, cancellationToken).ConfigureAwait(false);
        if (!opened.TryGetValue(out var tab))
        {
            return Result<LocatedTask>.Failure(opened.Error!);
        }

        var mapped = MapRows(tab);
        if (!mapped.TryGetValue(out var rows))
        {
            return Result<LocatedTask>.Failure(mapped.Error!);
        }

        for (var index = 0; index < rows.Tasks.Count; index++)
        {
            if (rows.Tasks[index].Id == id)
            {
                return Result<LocatedTask>.Success(new LocatedTask(tab, rows.DataRowIndexes[index], rows.Tasks[index]));
            }
        }

        return Result<LocatedTask>.Failure(
            TodoError.NotFound($"Task {id.Value} was not found in space '{space.Value}'."));
    }

    private static Result<MappedRows> MapRows(TaskTab tab)
    {
        var tasks = new List<TaskItem>(tab.DataRows.Count);
        var indexes = new List<int>(tab.DataRows.Count);

        for (var index = 0; index < tab.DataRows.Count; index++)
        {
            var row = tab.DataRows[index];

            if (TaskRowMapper.IsBlankRow(row))
            {
                continue;
            }

            var mapped = TaskRowMapper.FromRow(row, tab.Binding, TaskTab.RowNumber(index));
            if (!mapped.TryGetValue(out var task))
            {
                return Result<MappedRows>.Failure(mapped.Error!);
            }

            tasks.Add(task);
            indexes.Add(index);
        }

        return Result<MappedRows>.Success(new MappedRows(tasks, indexes));
    }

    /// <summary>
    /// The counter can lag behind the sheet if a previous append stored its row but failed to write
    /// the counter, so the larger of the two wins. Either value alone can hand out a duplicate id.
    /// </summary>
    private static int ResolveNextId(SpaceName space, IReadOnlyList<TaskItem> existing, MetaTable meta)
    {
        var counter = 0;

        if (meta.TryGet(TaskSchema.NextIdKey(space.Value), out var raw)
            && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stored))
        {
            counter = stored;
        }

        var highest = 0;

        foreach (var task in existing)
        {
            highest = Math.Max(highest, task.Id.Value);
        }

        return Math.Max(Math.Max(counter, highest + 1), 1);
    }

    /// <summary>
    /// The row is already on the sheet by the time this runs, so a failed counter write must not fail
    /// the add: the worst case is a gap in ids, not a lost task.
    /// </summary>
    private async Task PersistNextIdAsync(
        SpaceName space,
        int assignedId,
        MetaTable meta,
        CancellationToken cancellationToken)
    {
        var written = await _gateway
            .UpsertMetaAsync(
                TaskSchema.NextIdKey(space.Value),
                (assignedId + 1).ToString(CultureInfo.InvariantCulture),
                meta,
                cancellationToken)
            .ConfigureAwait(false);

        if (written.IsFailure)
        {
            SheetsLog.IdCounterNotSaved(_logger, assignedId, space.Value, written.Error!.Message);
        }
    }

    private sealed record MappedRows(IReadOnlyList<TaskItem> Tasks, IReadOnlyList<int> DataRowIndexes);

    private sealed record LocatedTask(TaskTab Tab, int DataRowIndex, TaskItem Task);
}
