using Todo.Application.Common;
using Todo.Application.Spaces;
using Todo.Domain.Spaces;

namespace Todo.Application.Tests.Fakes;

/// <summary>
/// In-memory space store. Every mutating call is counted so tests can assert that a service did
/// (or did not) reach the repository.
/// </summary>
public sealed class FakeSpaceRepository : ISpaceRepository
{
    private readonly List<SpaceName> _spaces = [];

    public FakeSpaceRepository(params SpaceName[] seeded) => _spaces.AddRange(seeded);

    public int ListCalls { get; private set; }

    public int ExistsCalls { get; private set; }

    public int CreateCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    public IReadOnlyList<SpaceName> Spaces => _spaces;

    /// <summary>When set, <see cref="CreateAsync"/> returns this failure instead of creating.</summary>
    public TodoError? CreateFailure { get; set; }

    /// <summary>When set, <see cref="DeleteAsync"/> returns this failure instead of deleting.</summary>
    public TodoError? DeleteFailure { get; set; }

    /// <summary>When set, <see cref="ExistsAsync"/> returns this failure.</summary>
    public TodoError? ExistsFailure { get; set; }

    public Task<Result<IReadOnlyList<SpaceName>>> ListAsync(CancellationToken cancellationToken)
    {
        ListCalls++;
        return Task.FromResult(Result<IReadOnlyList<SpaceName>>.Success([.. _spaces]));
    }

    public Task<Result<bool>> ExistsAsync(SpaceName space, CancellationToken cancellationToken)
    {
        ExistsCalls++;

        return Task.FromResult(ExistsFailure is { } error
            ? Result<bool>.Failure(error)
            : Result<bool>.Success(_spaces.Contains(space)));
    }

    public Task<Result> CreateAsync(SpaceName space, CancellationToken cancellationToken)
    {
        CreateCalls++;

        if (CreateFailure is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        _spaces.Add(space);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteAsync(SpaceName space, CancellationToken cancellationToken)
    {
        DeleteCalls++;

        if (DeleteFailure is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        return Task.FromResult(_spaces.Remove(space)
            ? Result.Success()
            : Result.Failure(TodoError.NotFound($"Space '{space}' was not found.")));
    }
}
