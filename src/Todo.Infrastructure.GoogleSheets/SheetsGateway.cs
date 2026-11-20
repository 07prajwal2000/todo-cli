using System.Globalization;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Todo.Application.Common;
using Todo.Domain.Tasks;
using Todo.Infrastructure.GoogleSheets.Mapping;
using BatchRequest = Google.Apis.Sheets.v4.Data.Request;

namespace Todo.Infrastructure.GoogleSheets;

public sealed record SheetInfo(string Title, int SheetId);

public sealed record SpreadsheetInfo(string? Title, IReadOnlyList<SheetInfo> Sheets)
{
    public SheetInfo? Find(string title) =>
        Sheets.FirstOrDefault(sheet => string.Equals(sheet.Title, title, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A task tab that is known to exist, carries the schema headers and has been read.</summary>
public sealed record TaskTab(
    SheetInfo Sheet,
    SheetLayout Layout,
    TaskSchemaBinding Binding,
    IReadOnlyList<IReadOnlyList<object?>> DataRows)
{
    /// <summary>A1 row number for a data row, so callers never recompute the header offset.</summary>
    public static int RowNumber(int dataRowIndex) => dataRowIndex + 2;
}

/// <summary>
/// The single place that talks to the Sheets API: sheet discovery, tab bootstrap, header
/// reconciliation, meta bookkeeping and row IO. Repositories compose these primitives so the SDK's
/// request/response shapes never leak past this type.
/// </summary>
public sealed class SheetsGateway
{
    private const string SpreadsheetFields = "properties.title,sheets.properties";

    private readonly SheetsConnectionFactory _connections;
    private readonly IRequestExecutor _executor;
    private readonly ILogger<SheetsGateway> _logger;

    public SheetsGateway(
        SheetsConnectionFactory connections,
        IRequestExecutor executor,
        ILogger<SheetsGateway>? logger = null)
    {
        _connections = connections;
        _executor = executor;
        _logger = logger ?? NullLogger<SheetsGateway>.Instance;
    }

    public async Task<Result<SpreadsheetInfo>> GetSpreadsheetAsync(CancellationToken cancellationToken)
    {
        var fetched = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var request = service.Spreadsheets.Get(spreadsheetId);
                request.IncludeGridData = false;
                request.Fields = SpreadsheetFields;
                return await request.ExecuteAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        if (!fetched.TryGetValue(out var spreadsheet))
        {
            return Result<SpreadsheetInfo>.Failure(fetched.Error!);
        }

        var sheets = new List<SheetInfo>();

        if (spreadsheet.Sheets is { } rawSheets)
        {
            foreach (var sheet in rawSheets)
            {
                if (sheet.Properties?.Title is { Length: > 0 } title)
                {
                    sheets.Add(new SheetInfo(title, sheet.Properties.SheetId ?? 0));
                }
            }
        }

        return Result<SpreadsheetInfo>.Success(new SpreadsheetInfo(spreadsheet.Properties?.Title, sheets));
    }

    public async Task<Result<SheetInfo>> GetSheetAsync(string title, CancellationToken cancellationToken)
    {
        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result<SheetInfo>.Failure(spreadsheet.Error!);
        }

        return info.Find(title) is { } sheet
            ? Result<SheetInfo>.Success(sheet)
            : Result<SheetInfo>.Failure(TodoError.NotFound($"The spreadsheet has no tab named '{title}'."));
    }

    public async Task<Result<SheetInfo>> AddSheetAsync(string title, CancellationToken cancellationToken)
    {
        var created = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var body = new BatchUpdateSpreadsheetRequest
                {
                    Requests =
                    [
                        new BatchRequest
                        {
                            AddSheet = new AddSheetRequest
                            {
                                Properties = new SheetProperties { Title = title },
                            },
                        },
                    ],
                };

                var request = service.Spreadsheets.BatchUpdate(body, spreadsheetId);
                return await request.ExecuteAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        if (created.TryGetValue(out var response))
        {
            var properties = response.Replies?.FirstOrDefault()?.AddSheet?.Properties;

            SheetsLog.SheetCreated(_logger, title);

            return Result<SheetInfo>.Success(new SheetInfo(
                properties?.Title ?? title,
                properties?.SheetId ?? 0));
        }

        // Two processes racing to bootstrap the same tab both get a 400; whoever lost simply adopts
        // the tab the winner created instead of surfacing a spurious conflict.
        if (created.Error!.Code == ErrorCode.Conflict)
        {
            var existing = await GetSheetAsync(title, cancellationToken).ConfigureAwait(false);

            if (existing.IsSuccess)
            {
                SheetsLog.SheetAdopted(_logger, title);
                return existing;
            }
        }

        return Result<SheetInfo>.Failure(created.Error!);
    }

    public async Task<Result> DeleteSheetAsync(string title, CancellationToken cancellationToken)
    {
        var sheet = await GetSheetAsync(title, cancellationToken).ConfigureAwait(false);
        if (!sheet.TryGetValue(out var info))
        {
            return Result.Failure(sheet.Error!);
        }

        var deleted = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var body = new BatchUpdateSpreadsheetRequest
                {
                    Requests =
                    [
                        new BatchRequest
                        {
                            DeleteSheet = new DeleteSheetRequest { SheetId = info.SheetId },
                        },
                    ],
                };

                return await service.Spreadsheets.BatchUpdate(body, spreadsheetId)
                    .ExecuteAsync(token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? Result.Success() : Result.Failure(deleted.Error!);
    }

    /// <summary>
    /// Opens a task tab: creates it and its headers when missing, and refuses to proceed when an
    /// existing tab contradicts the schema, because inventing columns would corrupt user data.
    /// </summary>
    public async Task<Result<TaskTab>> OpenTaskTabAsync(string tabTitle, CancellationToken cancellationToken)
    {
        var ensured = await EnsureTaskTabAsync(tabTitle, cancellationToken).ConfigureAwait(false);
        if (!ensured.TryGetValue(out var sheet))
        {
            return Result<TaskTab>.Failure(ensured.Error!);
        }

        var layout = SheetLayout.ForSchema(tabTitle);

        var values = await ReadValuesAsync(layout.ReadRange, cancellationToken).ConfigureAwait(false);
        if (!values.TryGetValue(out var rows))
        {
            return Result<TaskTab>.Failure(values.Error!);
        }

        if (rows.Count == 0)
        {
            return Result<TaskTab>.Failure(TodoError.Conflict(
                $"The '{tabTitle}' tab has no header row. Run 'todo doctor --fix'."));
        }

        var binding = TaskSchemaBinding.ValidateHeaders(rows[0]);
        if (!binding.TryGetValue(out var validated))
        {
            return Result<TaskTab>.Failure(binding.Error!);
        }

        return Result<TaskTab>.Success(new TaskTab(sheet, layout, validated, [.. rows.Skip(1)]));
    }

    public async Task<Result<MetaTable>> ReadMetaAsync(CancellationToken cancellationToken)
    {
        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result<MetaTable>.Failure(spreadsheet.Error!);
        }

        if (info.Find(TaskSchema.MetaTabName) is null)
        {
            return Result<MetaTable>.Success(MetaTable.Empty);
        }

        var values = await ReadValuesAsync(SheetLayout.ForMetaTab.TailRange, cancellationToken)
            .ConfigureAwait(false);

        if (!values.TryGetValue(out var rows) || rows.Count <= 1)
        {
            return values.IsSuccess
                ? Result<MetaTable>.Success(MetaTable.Empty)
                : Result<MetaTable>.Failure(values.Error!);
        }

        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rowNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < rows.Count; index++)
        {
            var key = AsText(rows[index], 0);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            entries[key.Trim()] = AsText(rows[index], 1) ?? string.Empty;
            rowNumbers[key.Trim()] = index + 1;
        }

        return Result<MetaTable>.Success(new MetaTable(entries, rowNumbers));
    }

    public async Task<Result> EnsureMetaAsync(CancellationToken cancellationToken)
    {
        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result.Failure(spreadsheet.Error!);
        }

        var layout = SheetLayout.ForMetaTab;

        if (info.Find(TaskSchema.MetaTabName) is null)
        {
            var added = await AddSheetAsync(TaskSchema.MetaTabName, cancellationToken).ConfigureAwait(false);
            if (!added.IsSuccess)
            {
                return Result.Failure(added.Error!);
            }

            var header = await WriteValuesAsync(
                layout.HeaderRange,
                [layout.Columns.Cast<object>().ToList()],
                cancellationToken).ConfigureAwait(false);

            if (!header.IsSuccess)
            {
                return Result.Failure(header.Error!);
            }
        }
        else
        {
            var existing = await ReadValuesAsync(layout.HeaderRange, cancellationToken).ConfigureAwait(false);
            if (!existing.IsSuccess)
            {
                return Result.Failure(existing.Error!);
            }

            if (existing.Value.Count == 0 || existing.Value[0].Count == 0)
            {
                var header = await WriteValuesAsync(
                    layout.HeaderRange,
                    [layout.Columns.Cast<object>().ToList()],
                    cancellationToken).ConfigureAwait(false);

                if (!header.IsSuccess)
                {
                    return Result.Failure(header.Error!);
                }
            }
        }

        var version = await ReadMetaAsync(cancellationToken).ConfigureAwait(false);
        if (!version.IsSuccess)
        {
            return Result.Failure(version.Error!);
        }

        if (version.Value.TryGet(TaskSchema.SchemaVersionKey, out _))
        {
            return Result.Success();
        }

        return await UpsertMetaAsync(
            TaskSchema.SchemaVersionKey,
            TaskSchema.Version.ToString(CultureInfo.InvariantCulture),
            version.Value,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> UpsertMetaAsync(
        string key,
        string value,
        MetaTable current,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (current.RowNumbers.TryGetValue(key, out var rowNumber))
        {
            var updated = await WriteValuesAsync(
                SheetLayout.ForMetaTab.RowRange(rowNumber),
                [[value]],
                cancellationToken).ConfigureAwait(false);

            return updated.IsSuccess ? Result.Success() : Result.Failure(updated.Error!);
        }

        var appended = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var body = new ValueRange { Values = [[key, value]] };
                var request = service.Spreadsheets.Values.Append(
                    body,
                    spreadsheetId,
                    SheetLayout.ForMetaTab.TailRange);

                request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest
                    .ValueInputOptionEnum.RAW;
                request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest
                    .InsertDataOptionEnum.INSERTROWS;
                request.IncludeValuesInResponse = false;

                return await request.ExecuteAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return appended.IsSuccess ? Result.Success() : Result.Failure(appended.Error!);
    }

    public async Task<Result<IReadOnlyList<IReadOnlyList<object?>>>> ReadValuesAsync(
        string range,
        CancellationToken cancellationToken)
    {
        var fetched = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                request.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest
                    .ValueRenderOptionEnum.UNFORMATTEDVALUE;
                request.MajorDimension = SpreadsheetsResource.ValuesResource.GetRequest
                    .MajorDimensionEnum.ROWS;

                return await request.ExecuteAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        if (!fetched.TryGetValue(out var valueRange))
        {
            return Result<IReadOnlyList<IReadOnlyList<object?>>>.Failure(fetched.Error!);
        }

        var rows = new List<IReadOnlyList<object?>>();

        if (valueRange.Values is { } values)
        {
            foreach (var row in values)
            {
                rows.Add(row is null ? [] : [.. row.Cast<object?>()]);
            }
        }

        return Result<IReadOnlyList<IReadOnlyList<object?>>>.Success(rows);
    }

    public async Task<Result> WriteValuesAsync(
        string range,
        IReadOnlyList<IReadOnlyList<object>> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);

        var updated = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var body = new ValueRange { Values = [.. values.Select(row => row.ToList())] };
                var request = service.Spreadsheets.Values.Update(body, spreadsheetId, range);
                request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest
                    .ValueInputOptionEnum.RAW;
                request.IncludeValuesInResponse = false;

                return await request.ExecuteAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return updated.IsSuccess ? Result.Success() : Result.Failure(updated.Error!);
    }

    public async Task<Result> AppendValuesAsync(
        string range,
        IReadOnlyList<object> row,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var appended = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var body = new ValueRange { Values = [row.ToList()] };
                var request = service.Spreadsheets.Values.Append(body, spreadsheetId, range);
                request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest
                    .ValueInputOptionEnum.RAW;
                request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest
                    .InsertDataOptionEnum.INSERTROWS;
                request.IncludeValuesInResponse = false;

                return await request.ExecuteAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return appended.IsSuccess ? Result.Success() : Result.Failure(appended.Error!);
    }

    public async Task<Result> DeleteRowsAsync(int sheetId, int rowNumber, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowNumber, 2, nameof(rowNumber));

        var deleted = await CallAsync(
            async (service, spreadsheetId, token) =>
            {
                var body = new BatchUpdateSpreadsheetRequest
                {
                    Requests =
                    [
                        new BatchRequest
                        {
                            DeleteDimension = new DeleteDimensionRequest
                            {
                                Range = new DimensionRange
                                {
                                    SheetId = sheetId,
                                    Dimension = "ROWS",
                                    StartIndex = rowNumber - 1,
                                    EndIndex = rowNumber,
                                },
                            },
                        },
                    ],
                };

                return await service.Spreadsheets.BatchUpdate(body, spreadsheetId)
                    .ExecuteAsync(token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? Result.Success() : Result.Failure(deleted.Error!);
    }

    public static string? AsText(IReadOnlyList<object?> row, int index) =>
        index < row.Count ? row[index] switch
        {
            null => null,
            string text => text,
            var other => Convert.ToString(other, CultureInfo.InvariantCulture),
        }
        : null;

    private async Task<Result<SheetInfo>> EnsureTaskTabAsync(string tabTitle, CancellationToken cancellationToken)
    {
        var spreadsheet = await GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result<SheetInfo>.Failure(spreadsheet.Error!);
        }

        var layout = SheetLayout.ForSchema(tabTitle);
        var sheet = info.Find(tabTitle);

        if (sheet is null)
        {
            var added = await AddSheetAsync(tabTitle, cancellationToken).ConfigureAwait(false);
            if (!added.TryGetValue(out sheet))
            {
                return Result<SheetInfo>.Failure(added.Error!);
            }

            var written = await WriteValuesAsync(
                layout.HeaderRange,
                [TaskRowMapper.HeaderRow().ToList()],
                cancellationToken).ConfigureAwait(false);

            if (!written.IsSuccess)
            {
                return Result<SheetInfo>.Failure(written.Error!);
            }
        }
        else
        {
            var header = await ReadValuesAsync(layout.HeaderRange, cancellationToken).ConfigureAwait(false);
            if (!header.TryGetValue(out var headerRows))
            {
                return Result<SheetInfo>.Failure(header.Error!);
            }

            if (headerRows.Count == 0 || headerRows[0].Count == 0)
            {
                var written = await WriteValuesAsync(
                    layout.HeaderRange,
                    [TaskRowMapper.HeaderRow().ToList()],
                    cancellationToken).ConfigureAwait(false);

                if (!written.IsSuccess)
                {
                    return Result<SheetInfo>.Failure(written.Error!);
                }
            }
        }

        var meta = await EnsureMetaAsync(cancellationToken).ConfigureAwait(false);

        return meta.IsSuccess
            ? Result<SheetInfo>.Success(sheet)
            : Result<SheetInfo>.Failure(meta.Error!);
    }

    private async Task<Result<T>> CallAsync<T>(
        Func<SheetsService, string, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var service = await _connections.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!service.TryGetValue(out var client))
        {
            return Result<T>.Failure(service.Error!);
        }

        var spreadsheetId = await _connections.GetSpreadsheetIdAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheetId.TryGetValue(out var id))
        {
            return Result<T>.Failure(spreadsheetId.Error!);
        }

        var result = await _executor
            .ExecuteAsync(token => operation(client, id, token), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? result
            : Result<T>.Failure(await EnrichAsync(result.Error!, id, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// A bare 403 leaves the user with no way forward, so the message names the account that needs
    /// edit access — the single most common Google Sheets setup mistake.
    /// </summary>
    private async Task<TodoError> EnrichAsync(TodoError error, string spreadsheetId, CancellationToken cancellationToken)
    {
        if (error.Code != ErrorCode.Permission)
        {
            return error;
        }

        var email = await _connections.GetServiceAccountEmailAsync(cancellationToken).ConfigureAwait(false);
        var who = email.TryGetValue(out var address) && !string.IsNullOrWhiteSpace(address)
            ? address
            : "the service account";

        return TodoError.Permission(
            $"Google Sheets denied access to spreadsheet '{spreadsheetId}'. " +
            $"Share the spreadsheet with {who} as Editor.");
    }
}

/// <summary>Raw <c>_meta</c> contents plus the A1 row of each key, so updates replace instead of duplicate.</summary>
public sealed record MetaTable(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, int> RowNumbers)
{
    public static MetaTable Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

    public bool TryGet(string key, out string value) => Values.TryGetValue(key, out value!);
}
