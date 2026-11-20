using Microsoft.AspNetCore.Http;
using Todo.Application.Common;
using Todo.Web.Contracts;

namespace Todo.Web.Http;

/// <summary>
/// The single translation from an application <see cref="TodoError"/> to an HTTP response, so a new
/// error code can never be mapped two different ways in two different endpoints.
/// </summary>
public static class ResultMapping
{
    public static int StatusCodeFor(ErrorCode code) => code switch
    {
        ErrorCode.None => StatusCodes.Status200OK,
        ErrorCode.Validation => StatusCodes.Status400BadRequest,
        ErrorCode.NotFound => StatusCodes.Status404NotFound,
        ErrorCode.Conflict => StatusCodes.Status409Conflict,
        ErrorCode.Configuration => StatusCodes.Status503ServiceUnavailable,
        ErrorCode.Permission => StatusCodes.Status403Forbidden,
        ErrorCode.Unavailable => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static ErrorResponse ToErrorResponse(TodoError error) => new()
    {
        Code = error.Code.ToString().ToLowerInvariant(),
        Message = error.Message,
        Fields = error.Fields is null
            ? null
            : new Dictionary<string, string[]>(error.Fields, StringComparer.Ordinal),
    };

    public static IResult ToHttpResult(TodoError error) =>
        Results.Json(
            ToErrorResponse(error),
            DashboardJsonContext.Default.ErrorResponse,
            statusCode: StatusCodeFor(error.Code));

    public static IResult ToHttpResult<T>(Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.TryGetValue(out var value) ? onSuccess(value) : ToHttpResult(result.Error!);
    }

    public static IResult ToHttpResult(Result result, Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess() : ToHttpResult(result.Error!);
    }

    public static IResult Validation(string message, string field) =>
        ToHttpResult(TodoError.Validation(message, new Dictionary<string, string[]> { [field] = [message] }));

    public static IResult Forbidden(string message) => ToHttpResult(TodoError.Permission(message));
}
