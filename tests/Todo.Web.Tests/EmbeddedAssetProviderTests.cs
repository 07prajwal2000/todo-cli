using System.Text;
using Todo.Web.Assets;

namespace Todo.Web.Tests;

public sealed class EmbeddedAssetProviderTests
{
    private readonly EmbeddedAssetProvider _provider = new(typeof(EmbeddedAssetProvider).Assembly);

    [Fact]
    public void RootResolvesToIndexHtml()
    {
        Assert.True(_provider.TryResolve("/", out var fileName));
        Assert.Equal("index.html", fileName);
    }

    [Fact]
    public async Task RootServesTheIndexDocumentAsHtml()
    {
        var (contentType, body) = await ReadAsync("/");

        Assert.StartsWith("text/html", contentType, StringComparison.Ordinal);
        Assert.Contains("<title>Todo Dashboard</title>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StylesheetIsServedAsCss()
    {
        var (contentType, body) = await ReadAsync("/app.css");

        Assert.Equal("text/css; charset=utf-8", contentType);
        Assert.Contains(".task-grid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScriptIsServedAsJavaScript()
    {
        var (contentType, body) = await ReadAsync("/app.js");

        Assert.Equal("text/javascript; charset=utf-8", contentType);
        Assert.Contains("X-Todo-Client", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PicoIsEmbeddedAndServedAsCss()
    {
        Assert.True(_provider.TryResolve("/pico.min.css", out var fileName));
        Assert.Equal("pico.min.css", fileName);
        Assert.Equal("text/css; charset=utf-8", EmbeddedAssetProvider.ContentTypeFor(fileName));
    }

    [Theory]
    [InlineData("/nope.js")]
    [InlineData("/assets/app.css")]
    [InlineData("/../appsettings.json")]
    public void UnknownPathsDoNotResolve(string path)
    {
        Assert.False(_provider.TryResolve(path, out var fileName));
        Assert.Empty(fileName);
    }

    [Fact]
    public async Task UnknownPathYieldsNotFoundThroughTheEndpoint()
    {
        var result = DashboardEndpointsServeAsset("/nope.js");

        var (status, _) = await ResultMappingTests.ExecuteAsync(result);

        Assert.Equal(404, status);
    }

    [Fact]
    public void ContentTypeMapCoversEveryEmbeddedFile()
    {
        foreach (var fileName in _provider.FileNames)
        {
            var contentType = EmbeddedAssetProvider.ContentTypeFor(fileName);

            Assert.NotEqual("application/octet-stream", contentType);
        }
    }

    [Fact]
    public void CacheControlIsNoCacheForTheEntryPageOnly()
    {
        Assert.Equal("no-cache", EmbeddedAssetProvider.CacheControlFor("index.html"));
        Assert.Equal("public, max-age=60", EmbeddedAssetProvider.CacheControlFor("app.css"));
    }

    private static Microsoft.AspNetCore.Http.IResult DashboardEndpointsServeAsset(string path) =>
        Todo.Web.Http.DashboardEndpoints.ServeAsset(path.TrimStart('/'), null, new EmbeddedAssetProvider());

    private async Task<(string ContentType, string Body)> ReadAsync(string path)
    {
        Assert.True(_provider.TryResolve(path, out var fileName));

        await using var stream = _provider.OpenRead(fileName);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return (EmbeddedAssetProvider.ContentTypeFor(fileName), await reader.ReadToEndAsync());
    }
}
