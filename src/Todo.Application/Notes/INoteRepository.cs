using Todo.Application.Common;
using Todo.Domain.Notes;

namespace Todo.Application.Notes;

public interface INoteRepository
{
    Task<Result<IReadOnlyList<Note>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<Note>> FindAsync(int id, CancellationToken cancellationToken);

    Task<Result<Note>> AddAsync(Note draft, CancellationToken cancellationToken);

    Task<Result<Note>> UpdateAsync(Note note, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken);
}
