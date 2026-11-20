using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Todo.Web.Http;

namespace Todo.Web.Tests;

public sealed class LocalRequestGuardTests
{
    private const int Port = 12000;

    [Fact]
    public void AllowsSameOriginJsonPostWithTheClientHeader()
    {
        var decision = LocalRequestGuard.Evaluate(
            HttpMethods.Post,
            Port,
            $"http://127.0.0.1:{Port}",
            "application/json",
            hasBody: true,
            clientHeader: "dashboard");

        Assert.True(decision.IsAllowed);
        Assert.Empty(decision.Message);
    }

    [Fact]
    public void AllowsALoopbackRequestWithoutAnOriginHeader()
    {
        var decision = LocalRequestGuard.Evaluate(HttpMethods.Patch, Port, null, "application/json; charset=utf-8", true, "dashboard");

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void AllowsLocalhostOriginOnTheSamePort()
    {
        var decision = LocalRequestGuard.Evaluate(
            HttpMethods.Delete,
            Port,
            $"http://localhost:{Port}",
            null,
            hasBody: false,
            clientHeader: "dashboard");

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void RejectsAMissingClientHeader()
    {
        var decision = LocalRequestGuard.Evaluate(HttpMethods.Post, Port, null, "application/json", true, null);

        Assert.False(decision.IsAllowed);
        Assert.Contains("X-Todo-Client", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnUnknownClientHeaderValue()
    {
        var decision = LocalRequestGuard.Evaluate(HttpMethods.Post, Port, null, "application/json", true, "cli");

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void RejectsCrossOriginRequests()
    {
        var decision = LocalRequestGuard.Evaluate(
            HttpMethods.Post,
            Port,
            "https://evil.example",
            "application/json",
            true,
            "dashboard");

        Assert.False(decision.IsAllowed);
        Assert.Contains("Cross-origin", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsSameHostOnADifferentPort()
    {
        var decision = LocalRequestGuard.Evaluate(
            HttpMethods.Post,
            Port,
            "http://127.0.0.1:12345",
            "application/json",
            true,
            "dashboard");

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void RejectsNonJsonContentTypeForARequestWithABody()
    {
        var decision = LocalRequestGuard.Evaluate(
            HttpMethods.Post,
            Port,
            null,
            "text/plain",
            hasBody: true,
            clientHeader: "dashboard");

        Assert.False(decision.IsAllowed);
        Assert.Contains("application/json", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAMissingContentTypeForARequestWithABody()
    {
        var decision = LocalRequestGuard.Evaluate(HttpMethods.Post, Port, null, null, true, "dashboard");

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void IgnoresContentTypeForRequestsWithoutABody()
    {
        var decision = LocalRequestGuard.Evaluate(HttpMethods.Delete, Port, null, null, hasBody: false, "dashboard");

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void ReadRequestsAreNeverGuarded()
    {
        var decision = LocalRequestGuard.Evaluate(HttpMethods.Get, Port, "https://evil.example", null, false, null);

        Assert.True(decision.IsAllowed);
    }

    [Theory]
    [InlineData("POST", true)]
    [InlineData("post", true)]
    [InlineData("PATCH", true)]
    [InlineData("DELETE", true)]
    [InlineData("PUT", true)]
    [InlineData("GET", false)]
    [InlineData("HEAD", false)]
    [InlineData(null, false)]
    public void IsMutatingMethodClassifiesHttpMethods(string? method, bool expected)
    {
        Assert.Equal(expected, LocalRequestGuard.IsMutatingMethod(method));
    }

    [Fact]
    public void IsSameOriginAcceptsAFallbackPortMatch()
    {
        Assert.True(LocalRequestGuard.IsSameOrigin("http://127.0.0.1:5000/", 5000));
        Assert.False(LocalRequestGuard.IsSameOrigin("http://127.0.0.1:5001", 5000));
        Assert.False(LocalRequestGuard.IsSameOrigin(null, 5000));
        Assert.False(LocalRequestGuard.IsSameOrigin("http://10.0.0.1:5000", 5000));
    }

    [Fact]
    public async Task MiddlewareReturns403WithThePermissionErrorBody()
    {
        var middleware = new LocalRequestGuardMiddleware(_ => throw new InvalidOperationException("should not run"));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = 2;
        using var stream = new MemoryStream();
        context.Response.Body = stream;
        using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        context.RequestServices = services;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        Assert.Equal("permission", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task MiddlewareLetsAValidRequestThrough()
    {
        var reached = false;
        var middleware = new LocalRequestGuardMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Patch;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = 2;
        context.Request.Headers[LocalRequestGuard.ClientHeaderName] = LocalRequestGuard.ClientHeaderValue;

        await middleware.InvokeAsync(context);

        Assert.True(reached);
        Assert.Equal(200, context.Response.StatusCode);
    }
}
