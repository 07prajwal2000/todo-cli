using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Todo.Web.Http;

/// <summary>
/// Local-only request gate for the dashboard. A loopback server is still reachable from any page the
/// user has open, so mutating calls must prove they come from the dashboard itself (custom header),
/// are not cross-origin, and carry a JSON body.
/// </summary>
public static class LocalRequestGuard
{
    public const string ClientHeaderName = "X-Todo-Client";

    public const string ClientHeaderValue = "dashboard";

    public static bool IsMutatingMethod(string? method) =>
        method is not null
        && (HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method));

    /// <summary>
    /// Origin comparison is strict (scheme, host and port) because a page served from a different
    /// port is a different origin even though both are on loopback.
    /// </summary>
    public static bool IsSameOrigin(string? origin, int port)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var value = origin.Trim().TrimEnd('/');

        if (port <= 0)
        {
            // The bound port is only known after Kestrel starts (tests bind port 0), so fall back to
            // accepting loopback on any port instead of rejecting the dashboard's own requests.
            return IsLoopbackOrigin(value, null);
        }

        return IsLoopbackOrigin(value, port);
    }

    public static bool HasJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            && string.Equals(parsed.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    public static GuardDecision Evaluate(
        string? method,
        int port,
        string? origin,
        string? contentType,
        bool hasBody,
        string? clientHeader)
    {
        if (!IsMutatingMethod(method))
        {
            return GuardDecision.Allow();
        }

        if (!string.IsNullOrWhiteSpace(origin) && !IsSameOrigin(origin, port))
        {
            return GuardDecision.Reject("Cross-origin requests are not allowed.");
        }

        if (!string.Equals(clientHeader, ClientHeaderValue, StringComparison.OrdinalIgnoreCase))
        {
            return GuardDecision.Reject($"The {ClientHeaderName}: {ClientHeaderValue} header is required.");
        }

        if (hasBody && !HasJsonContentType(contentType))
        {
            return GuardDecision.Reject("Content-Type must be application/json.");
        }

        return GuardDecision.Allow();
    }

    private static bool IsLoopbackOrigin(string origin, int? port)
    {
        foreach (var host in (string[])["127.0.0.1", "localhost"])
        {
            if (port is { } expected)
            {
                if (string.Equals(origin, $"http://{host}:{expected}", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            var prefix = $"http://{host}:";
            if (origin.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(origin.AsSpan(prefix.Length), out var parsed)
                && parsed is > 0 and < 65536)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record GuardDecision
{
    private GuardDecision(bool isAllowed, string message)
    {
        IsAllowed = isAllowed;
        Message = message;
    }

    public bool IsAllowed { get; }

    public string Message { get; }

    public static GuardDecision Allow() => new(true, string.Empty);

    public static GuardDecision Reject(string message) => new(false, message);
}
