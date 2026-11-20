using Todo.Application.Common;
using Todo.Domain.Notes;

namespace Todo.Application.Notes;

public sealed class NoteService(INoteRepository repository, IClock clock)
{
    /// <summary>Newest first; <paramref name="search"/> matches title or content, case-insensitively.</summary>
    public async Task<Result<IReadOnlyList<Note>>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var listed = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!listed.TryGetValue(out var notes))
        {
            return listed;
        }

        var term = search?.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? notes
            : notes.Where(note =>
                note.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || note.Content.Contains(term, StringComparison.OrdinalIgnoreCase));

        return Result<IReadOnlyList<Note>>.Success([.. filtered.OrderByDescending(note => note.Id)]);
    }

    public Task<Result<Note>> GetAsync(int id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, cancellationToken);

    public async Task<Result<Note>> AddAsync(string? title, string? content, CancellationToken cancellationToken)
    {
        if (NoteSchema.Validate(title, content, partial: false) is { } invalid)
        {
            return TodoError.Validation("The note is not valid.", invalid.ByField);
        }

        return await repository
            .AddAsync(
                new Note { Id = 0, Title = title!.Trim(), Content = content ?? string.Empty, CreatedAt = clock.UtcNow },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Null arguments mean "leave as is".</summary>
    public async Task<Result<Note>> EditAsync(int id, string? title, string? content, CancellationToken cancellationToken)
    {
        if (NoteSchema.Validate(title, content, partial: true) is { } invalid)
        {
            return TodoError.Validation("The note is not valid.", invalid.ByField);
        }

        var found = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var note))
        {
            return found;
        }

        var updated = note with { Title = title?.Trim() ?? note.Title, Content = content ?? note.Content };

        return updated == note
            ? note
            : await repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result> DeleteAsync(int id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(id, cancellationToken);
}
