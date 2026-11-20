using Todo.Application.Common;
using Todo.Application.Notes;
using Todo.Domain.Notes;
using Todo.Infrastructure.GoogleSheets.Mapping;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Notes on a single <c>notes</c> tab. Like tasks, row numbers are resolved from the sheet on every
/// call. Ids are <c>max + 1</c>, so deleting the newest note frees its id.
/// </summary>
// ponytail: no counter in _meta; add one if id reuse after deleting the newest note ever matters.
public sealed class SheetsNoteRepository(SheetsGateway gateway) : INoteRepository
{
    private static readonly SheetLayout Layout = SheetLayout.ForTab(NoteSchema.TabName, NoteSchema.Columns);

    public async Task<Result<IReadOnlyList<Note>>> ListAsync(CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return opened.IsSuccess
            ? Result<IReadOnlyList<Note>>.Success([.. opened.Value.Notes.Select(entry => entry.Note)])
            : Result<IReadOnlyList<Note>>.Failure(opened.Error!);
    }

    public async Task<Result<Note>> FindAsync(int id, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(id, cancellationToken).ConfigureAwait(false);
        return located.IsSuccess ? Result<Note>.Success(located.Value.Entry.Note) : Result<Note>.Failure(located.Error!);
    }

    public async Task<Result<Note>> AddAsync(Note draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!opened.TryGetValue(out var tab))
        {
            return Result<Note>.Failure(opened.Error!);
        }

        var note = draft with { Id = tab.Notes.Select(entry => entry.Note.Id).DefaultIfEmpty(0).Max() + 1 };

        var appended = await gateway.AppendValuesAsync(Layout.TailRange, NoteRowMapper.ToRow(note), cancellationToken).ConfigureAwait(false);
        return appended.IsSuccess ? Result<Note>.Success(note) : Result<Note>.Failure(appended.Error!);
    }

    public async Task<Result<Note>> UpdateAsync(Note note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(note);

        var located = await LocateAsync(note.Id, cancellationToken).ConfigureAwait(false);
        if (!located.TryGetValue(out var position))
        {
            return Result<Note>.Failure(located.Error!);
        }

        var written = await gateway
            .WriteValuesAsync(Layout.RowRange(position.Entry.RowNumber), [NoteRowMapper.ToRow(note)], cancellationToken)
            .ConfigureAwait(false);

        return written.IsSuccess ? Result<Note>.Success(note) : Result<Note>.Failure(written.Error!);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(id, cancellationToken).ConfigureAwait(false);
        if (!located.TryGetValue(out var position))
        {
            return Result.Failure(located.Error!);
        }

        return await gateway
            .DeleteRowsAsync(position.Sheet.SheetId, position.Entry.RowNumber, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<NotesTab>> OpenAsync(CancellationToken cancellationToken)
    {
        var spreadsheet = await gateway.GetSpreadsheetAsync(cancellationToken).ConfigureAwait(false);
        if (!spreadsheet.TryGetValue(out var info))
        {
            return Result<NotesTab>.Failure(spreadsheet.Error!);
        }

        var sheet = info.Find(NoteSchema.TabName);

        if (sheet is null)
        {
            var added = await gateway.AddSheetAsync(NoteSchema.TabName, cancellationToken).ConfigureAwait(false);
            if (!added.TryGetValue(out sheet))
            {
                return Result<NotesTab>.Failure(added.Error!);
            }
        }

        var values = await gateway.ReadValuesAsync(Layout.ReadRange, cancellationToken).ConfigureAwait(false);
        if (!values.TryGetValue(out var rows))
        {
            return Result<NotesTab>.Failure(values.Error!);
        }

        if (rows.Count == 0 || rows[0].Count == 0)
        {
            var header = await gateway
                .WriteValuesAsync(Layout.HeaderRange, [[.. NoteSchema.Columns]], cancellationToken)
                .ConfigureAwait(false);

            return header.IsSuccess
                ? Result<NotesTab>.Success(new NotesTab(sheet, []))
                : Result<NotesTab>.Failure(header.Error!);
        }

        for (var index = 0; index < NoteSchema.Columns.Count; index++)
        {
            if (!string.Equals(SheetsGateway.AsText(rows[0], index)?.Trim(), NoteSchema.Columns[index], StringComparison.OrdinalIgnoreCase))
            {
                return TodoError.Conflict(
                    $"The '{NoteSchema.TabName}' tab must start with the columns {string.Join(", ", NoteSchema.Columns)}. " +
                    "Rename or move any existing tab with that name.");
            }
        }

        var notes = new List<NoteRow>();

        for (var index = 1; index < rows.Count; index++)
        {
            if (TaskRowMapper.IsBlankRow(rows[index]))
            {
                continue;
            }

            var mapped = NoteRowMapper.FromRow(rows[index], index + 1);
            if (!mapped.TryGetValue(out var note))
            {
                return Result<NotesTab>.Failure(mapped.Error!);
            }

            notes.Add(new NoteRow(note, index + 1));
        }

        return Result<NotesTab>.Success(new NotesTab(sheet, notes));
    }

    private async Task<Result<Located>> LocateAsync(int id, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!opened.TryGetValue(out var tab))
        {
            return Result<Located>.Failure(opened.Error!);
        }

        return tab.Notes.FirstOrDefault(entry => entry.Note.Id == id) is { } entry
            ? Result<Located>.Success(new Located(tab.Sheet, entry))
            : Result<Located>.Failure(TodoError.NotFound($"Note {id} was not found."));
    }

    /// <summary><paramref name="RowNumber"/> is the 1-based A1 row, header included.</summary>
    private sealed record NoteRow(Note Note, int RowNumber);

    private sealed record NotesTab(SheetInfo Sheet, IReadOnlyList<NoteRow> Notes);

    private sealed record Located(SheetInfo Sheet, NoteRow Entry);
}
