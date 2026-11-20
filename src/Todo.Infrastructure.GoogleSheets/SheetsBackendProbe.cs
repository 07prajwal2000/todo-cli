using System.Globalization;
using Todo.Application.Common;
using Todo.Application.Spaces;
using Todo.Domain.Tasks;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Read-only health check behind <c>todo doctor</c>. It reports what the sheet actually contains so
/// setup failures are diagnosable without a debugger.
/// </summary>
public sealed class SheetsBackendProbe : IBackendProbe
{
    private readonly SheetsGateway _gateway;
    private readonly SheetsConnectionFactory _connections;

    public SheetsBackendProbe(SheetsGateway gateway, SheetsConnectionFactory connections)
    {
        _gateway = gateway;
        _connections = connections;
    }

    public async Task<Result<BackendProbeResult>> ProbeAsync(CancellationToken cancellationToken)
    {
        var spreadsheet = await _gateway.GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result<BackendProbeResult>.Failure(spreadsheet.Error!);
        }

        var meta = await _gateway.ReadMetaAsync(cancellationToken).ConfigureAwait(false);
        if (!meta.TryGetValue(out var metaTable))
        {
            return Result<BackendProbeResult>.Failure(meta.Error!);
        }

        var schemaVersion = ReadSchemaVersion(metaTable);
        var schemaReady = schemaVersion == TaskSchema.Version
            && await HeadersAreValidAsync(info, cancellationToken).ConfigureAwait(false);

        var email = await _connections.GetServiceAccountEmailAsync(cancellationToken).ConfigureAwait(false);

        return Result<BackendProbeResult>.Success(new BackendProbeResult
        {
            SpreadsheetTitle = info.Title ?? string.Empty,
            Tabs = [.. info.Sheets.Select(sheet => sheet.Title)],
            SchemaVersion = schemaVersion,
            ServiceAccountEmail = email.IsSuccess ? email.Value : null,
            SchemaReady = schemaReady,
        });
    }

    /// <summary>
    /// Repairs only what is safe to infer: a missing header row, or missing required header cells at
    /// the end of an existing header row. Existing column positions are never moved, because moving
    /// them would silently re-associate data with the wrong field.
    /// </summary>
    public async Task<Result<BackendRepairResult>> RepairAsync(CancellationToken cancellationToken)
    {
        var spreadsheet = await _gateway.GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result<BackendRepairResult>.Failure(spreadsheet.Error!);
        }

        var actions = new List<string>();

        var meta = await _gateway.EnsureMetaAsync(cancellationToken).ConfigureAwait(false);
        if (meta.IsFailure)
        {
            return Result<BackendRepairResult>.Failure(meta.Error!);
        }

        actions.Add($"Ensured '{TaskSchema.MetaTabName}' tab and schema_version.");

        foreach (var sheet in info.Sheets)
        {
            if (!IsTaskTab(sheet.Title))
            {
                continue;
            }

            var layout = SheetLayout.ForSchema(sheet.Title);

            var header = await _gateway
                .ReadValuesAsync(layout.HeaderRange, cancellationToken)
                .ConfigureAwait(false);

            if (!header.TryGetValue(out var rows))
            {
                return Result<BackendRepairResult>.Failure(header.Error!);
            }

            var existing = rows.Count > 0 ? rows[0] : [];

            if (existing.Count == 0)
            {
                var written = await _gateway.WriteValuesAsync(
                    layout.HeaderRange,
                    [TaskRowMapper.HeaderRow().ToList()],
                    cancellationToken).ConfigureAwait(false);

                if (written.IsFailure)
                {
                    return Result<BackendRepairResult>.Failure(written.Error!);
                }

                actions.Add($"Wrote the header row of '{sheet.Title}'.");
                continue;
            }

            var present = existing
                .Select(cell => (cell as string)?.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = TaskSchema.Columns
                .Where(column => !present.Contains(column))
                .ToList();

            if (missing.Count == 0)
            {
                continue;
            }

            var startColumn = existing.Count + 1;
            var range = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{layout.QuotedTabName}!{SheetLayout.ColumnLetter(startColumn)}1:" +
                $"{SheetLayout.ColumnLetter(startColumn + missing.Count - 1)}1");

            var appended = await _gateway.WriteValuesAsync(
                range,
                [missing.Cast<object>().ToList()],
                cancellationToken).ConfigureAwait(false);

            if (appended.IsFailure)
            {
                return Result<BackendRepairResult>.Failure(appended.Error!);
            }

            actions.Add($"Appended missing header(s) to '{sheet.Title}': {string.Join(", ", missing)}.");
        }

        return Result<BackendRepairResult>.Success(new BackendRepairResult { Actions = actions });
    }

    private static int ReadSchemaVersion(MetaTable meta) =>
        meta.TryGet(TaskSchema.SchemaVersionKey, out var raw)
        && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            ? version
            : 0;

    private async Task<bool> HeadersAreValidAsync(SpreadsheetInfo info, CancellationToken cancellationToken)
    {
        foreach (var sheet in info.Sheets)
        {
            if (!IsTaskTab(sheet.Title))
            {
                continue;
            }

            var header = await _gateway
                .ReadValuesAsync(SheetLayout.ForSchema(sheet.Title).HeaderRange, cancellationToken)
                .ConfigureAwait(false);

            if (!header.TryGetValue(out var rows)
                || rows.Count == 0
                || !TaskSchemaBinding.ValidateHeaders(rows[0]).IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTaskTab(string title) =>
        !string.Equals(title, TaskSchema.MetaTabName, StringComparison.OrdinalIgnoreCase)
        && !title.StartsWith('_');
}
