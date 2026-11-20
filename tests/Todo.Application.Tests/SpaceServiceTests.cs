using Todo.Application.Common;
using Todo.Application.Spaces;
using Todo.Application.Tests.Fakes;
using Todo.Domain.Spaces;

namespace Todo.Application.Tests;

public sealed class SpaceServiceTests
{
    private readonly FakeSpaceRepository _repository = new(SpaceName.Default);
    private readonly SpaceService _service;

    public SpaceServiceTests() => _service = new SpaceService(_repository);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/name")]
    [InlineData("_reserved")]
    public async Task CreateAsyncRejectsAnInvalidNameKeyedOnTheSpaceField(string? name)
    {
        var result = await _service.CreateAsync(name, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.NotNull(result.Error!.Fields);
        Assert.True(result.Error!.Fields!.ContainsKey("space"));
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateAsyncRejectsAnAlreadyExistingSpace()
    {
        var result = await _service.CreateAsync("default", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Conflict, result.Error!.Code);
        Assert.Contains("default", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateAsyncCreatesANewSpace()
    {
        var result = await _service.CreateAsync("work", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("work", result.Value.Value);
        Assert.Equal(1, _repository.CreateCalls);
        Assert.Contains(SpaceName.Create("work"), _repository.Spaces);
    }

    [Theory]
    [InlineData("  work  ", "work")]
    [InlineData(" my space ", "my space")]
    public async Task CreateAsyncTrimsAnUnusualButValidName(string name, string expected)
    {
        var result = await _service.CreateAsync(name, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
        Assert.Contains(SpaceName.Create(expected), _repository.Spaces);
    }

    [Fact]
    public async Task CreateAsyncPropagatesAnExistenceCheckFailure()
    {
        _repository.ExistsFailure = TodoError.Unavailable("Sheet is down.");

        var result = await _service.CreateAsync("work", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateAsyncPropagatesACreateFailure()
    {
        _repository.CreateFailure = TodoError.Permission("No write access.");

        var result = await _service.CreateAsync("work", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Permission, result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsyncRefusesToRemoveTheDefaultSpace()
    {
        var result = await _service.DeleteAsync("default", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.Contains("default", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _repository.DeleteCalls);
    }

    [Fact]
    public async Task DeleteAsyncRefusesToRemoveTheDefaultSpaceCaseInsensitively()
    {
        var result = await _service.DeleteAsync("DEFAULT", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.Equal(0, _repository.DeleteCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad/name")]
    public async Task DeleteAsyncRejectsAnInvalidName(string? name)
    {
        var result = await _service.DeleteAsync(name, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.Equal(0, _repository.DeleteCalls);
    }

    [Fact]
    public async Task DeleteAsyncPropagatesTheRepositoryNotFound()
    {
        var result = await _service.DeleteAsync("missing", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.NotFound, result.Error!.Code);
        Assert.Equal(1, _repository.DeleteCalls);
    }

    [Fact]
    public async Task DeleteAsyncRemovesAnExistingNonDefaultSpace()
    {
        await _service.CreateAsync("work", CancellationToken.None);

        var result = await _service.DeleteAsync("work", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _repository.DeleteCalls);
        Assert.DoesNotContain(SpaceName.Create("work"), _repository.Spaces);
    }

    [Fact]
    public async Task EnsureExistsAsyncDoesNotCreateAnExistingSpace()
    {
        var result = await _service.EnsureExistsAsync(SpaceName.Default, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _repository.ExistsCalls);
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task EnsureExistsAsyncCreatesAMissingSpace()
    {
        var result = await _service.EnsureExistsAsync(SpaceName.Create("work"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _repository.ExistsCalls);
        Assert.Equal(1, _repository.CreateCalls);
        Assert.Contains(SpaceName.Create("work"), _repository.Spaces);
    }

    [Fact]
    public async Task EnsureExistsAsyncPropagatesAnExistenceCheckFailure()
    {
        _repository.ExistsFailure = TodoError.Unavailable("Sheet is down.");

        var result = await _service.EnsureExistsAsync(SpaceName.Create("work"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task ListAsyncSortsNamesCaseInsensitively()
    {
        await _service.CreateAsync("zulu", CancellationToken.None);
        await _service.CreateAsync("Alpha", CancellationToken.None);
        await _service.CreateAsync("mike", CancellationToken.None);
        await _service.CreateAsync("bravo", CancellationToken.None);

        var result = await _service.ListAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Alpha", "bravo", "default", "mike", "zulu"], result.Value.Select(name => name.Value));
    }
}
