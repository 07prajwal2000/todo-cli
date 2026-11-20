using Todo.Application.Common;
using Todo.Application.Spaces;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>Spaces are sheet tabs; every tab except the internal <c>_meta</c> tab is a space.</summary>
public sealed class SheetsSpaceRepository : ISpaceRepository
{
    private readonly SheetsGateway _gateway;

    public SheetsSpaceRepository(SheetsGateway gateway) => _gateway = gateway;

    public async Task<Result<IReadOnlyList<SpaceName>>> ListAsync(CancellationToken cancellationToken)
    {
        var spreadsheet = await _gateway.GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result<IReadOnlyList<SpaceName>>.Failure(spreadsheet.Error!);
        }

        var spaces = new List<SpaceName>(info.Sheets.Count);

        foreach (var sheet in info.Sheets)
        {
            if (IsSpaceTab(sheet.Title) && SpaceName.TryCreate(sheet.Title) is { } space)
            {
                spaces.Add(space);
            }
        }

        return Result<IReadOnlyList<SpaceName>>.Success(spaces);
    }

    public async Task<Result<bool>> ExistsAsync(SpaceName space, CancellationToken cancellationToken)
    {
        var listed = await ListAsync(cancellationToken).ConfigureAwait(false);

        return listed.IsSuccess
            ? Result<bool>.Success(listed.Value.Contains(space))
            : Result<bool>.Failure(listed.Error!);
    }

    public async Task<Result> CreateAsync(SpaceName space, CancellationToken cancellationToken)
    {
        if (!SpaceName.IsValid(space.Value, out var error))
        {
            return TodoError.Validation(error!);
        }

        if (!IsSpaceTab(space.Value))
        {
            return TodoError.Validation($"'{space.Value}' is reserved and cannot be used as a space name.");
        }

        var spreadsheet = await _gateway.GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result.Failure(spreadsheet.Error!);
        }

        if (info.Find(space.Value) is not null)
        {
            return TodoError.Conflict($"Space '{space.Value}' already exists.");
        }

        var layout = SheetLayout.ForSchema(space.Value);
        var created = await _gateway.AddSheetAsync(space.Value, cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess)
        {
            return Result.Failure(created.Error!);
        }

        return await _gateway
            .WriteValuesAsync(layout.HeaderRange, [TaskRowMapper.HeaderRow().ToList()], cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result> DeleteAsync(SpaceName space, CancellationToken cancellationToken)
    {
        if (space.IsDefault)
        {
            return TodoError.Validation("The default space cannot be removed.");
        }

        if (!IsSpaceTab(space.Value))
        {
            return TodoError.Validation($"'{space.Value}' is not a space and cannot be removed.");
        }

        return await _gateway.DeleteSheetAsync(space.Value, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSpaceTab(string title) =>
        !string.Equals(title, TaskSchema.MetaTabName, StringComparison.OrdinalIgnoreCase)
        && !title.StartsWith('_');
}
