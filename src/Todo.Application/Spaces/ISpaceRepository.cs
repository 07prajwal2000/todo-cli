using Todo.Application.Common;
using Todo.Domain.Spaces;

namespace Todo.Application.Spaces;

public interface ISpaceRepository
{
    Task<Result<IReadOnlyList<SpaceName>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<bool>> ExistsAsync(SpaceName space, CancellationToken cancellationToken);

    Task<Result> CreateAsync(SpaceName space, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(SpaceName space, CancellationToken cancellationToken);
}
