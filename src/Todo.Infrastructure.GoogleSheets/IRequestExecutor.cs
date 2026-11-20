using Todo.Application.Common;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Wraps every call to the Google Sheets API so transport concerns — retries, backoff and the
/// mapping from SDK exceptions to <see cref="TodoError"/> — live in one place. Ports stay free of
/// <c>Google</c> types and never see an exception for an expected failure.
/// </summary>
public interface IRequestExecutor
{
    Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
