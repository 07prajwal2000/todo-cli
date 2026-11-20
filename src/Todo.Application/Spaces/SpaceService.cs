using Todo.Application.Common;
using Todo.Domain.Spaces;

namespace Todo.Application.Spaces;

public sealed class SpaceService(ISpaceRepository repository)
{
    public async Task<Result<IReadOnlyList<SpaceName>>> ListAsync(CancellationToken cancellationToken)
    {
        var listed = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return listed.IsSuccess
            ? Result<IReadOnlyList<SpaceName>>.Success([.. listed.Value.OrderBy(name => name.Value, StringComparer.OrdinalIgnoreCase)])
            : listed;
    }

    public async Task<Result<SpaceName>> CreateAsync(string? name, CancellationToken cancellationToken)
    {
        if (!SpaceName.IsValid(name, out var error))
        {
            return Result<SpaceName>.Failure(
                TodoError.Validation(error!, new Dictionary<string, string[]> { ["space"] = [error!] }));
        }

        var space = SpaceName.Create(name!);

        var exists = await repository.ExistsAsync(space, cancellationToken).ConfigureAwait(false);
        if (!exists.TryGetValue(out var alreadyExists))
        {
            return Result<SpaceName>.Failure(exists.Error!);
        }

        if (alreadyExists)
        {
            return Result<SpaceName>.Failure(TodoError.Conflict($"Space '{space}' already exists."));
        }

        var created = await repository.CreateAsync(space, cancellationToken).ConfigureAwait(false);
        return created.IsSuccess ? Result<SpaceName>.Success(space) : Result<SpaceName>.Failure(created.Error!);
    }

    public async Task<Result> DeleteAsync(string? name, CancellationToken cancellationToken)
    {
        if (!SpaceName.IsValid(name, out var error))
        {
            return TodoError.Validation(error!);
        }

        var space = SpaceName.Create(name!);

        if (space.IsDefault)
        {
            return TodoError.Validation("The default space cannot be removed.");
        }

        return await repository.DeleteAsync(space, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> EnsureExistsAsync(SpaceName space, CancellationToken cancellationToken)
    {
        var exists = await repository.ExistsAsync(space, cancellationToken).ConfigureAwait(false);
        if (!exists.TryGetValue(out var found))
        {
            return exists.Error!;
        }

        return found ? Result.Success() : await repository.CreateAsync(space, cancellationToken).ConfigureAwait(false);
    }
}
