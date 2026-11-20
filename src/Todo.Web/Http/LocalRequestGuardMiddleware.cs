using Microsoft.AspNetCore.Http;
using Todo.Application.Common;
using Todo.Web.Contracts;

namespace Todo.Web.Http;

/// <summary>
/// Applies <see cref="LocalRequestGuard"/> to every request. The port comes from the live connection
/// rather than configuration so the comparison stays correct when Kestrel binds an ephemeral port.
/// </summary>
internal sealed class LocalRequestGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        var decision = LocalRequestGuard.Evaluate(
            request.Method,
            context.Connection.LocalPort,
            request.Headers.Origin,
            request.ContentType,
            HasBody(request),
            request.Headers[LocalRequestGuard.ClientHeaderName]);

        if (!decision.IsAllowed)
        {
            await Results.Json(
                ResultMapping.ToErrorResponse(TodoError.Permission(decision.Message)),
                DashboardJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context).ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool HasBody(HttpRequest request) =>
        request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding");
}
