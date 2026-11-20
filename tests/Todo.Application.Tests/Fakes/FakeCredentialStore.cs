using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Application.Tests.Fakes;

/// <summary>Records the last credential blob written and can be told to fail the write.</summary>
public sealed class FakeCredentialStore : ICredentialStore
{
    public int WriteCalls { get; private set; }

    public int ReadCalls { get; private set; }

    public string? Written { get; private set; }

    public TodoError? WriteFailure { get; set; }

    public string? Raw { get; set; }

    public Task<Result<string>> ReadRawAsync(CancellationToken cancellationToken)
    {
        ReadCalls++;

        return Task.FromResult(Raw is null
            ? Result<string>.Failure(TodoError.Configuration("The credentials file does not exist."))
            : Result<string>.Success(Raw));
    }

    public Task<Result> WriteRawAsync(string json, CancellationToken cancellationToken)
    {
        WriteCalls++;

        if (WriteFailure is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        Written = json;
        Raw = json;
        return Task.FromResult(Result.Success());
    }

    public string DescribeLocation() => "/fake/credentials.json";
}
