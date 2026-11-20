using System.Net;
using System.Text.Json;
using Todo.Web.Contracts;

namespace Todo.Web.Tests;

public sealed class NoteEndpointTests
{
    private static async Task<(HttpStatusCode Status, JsonDocument Json)> SendAsync(
        HttpClient client, HttpMethod method, string url, object? body = null)
    {
        HttpContent? content = body switch
        {
            null => null,
            CreateNoteRequest create => DashboardHarness.JsonBody(create),
            UpdateNoteRequest update => DashboardHarness.JsonBody(update),
            _ => throw new ArgumentException("Unsupported body.", nameof(body)),
        };

        using var request = DashboardHarness.Request(method, url, content);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(text.Length == 0 ? "null" : text));
    }

    [Fact]
    public async Task CrudRoundTripKeepsMarkdownVerbatim()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();
        const string markdown = "# Heading\n\n- [x] done\n\n```cs\nvar a = 1 < 2;\n```\n<script>alert(1)</script>";

        var (created, createdJson) = await SendAsync(client, HttpMethod.Post, "/api/notes", new CreateNoteRequest { Title = "  First  ", Content = markdown });
        Assert.Equal(HttpStatusCode.Created, created);
        Assert.Equal(1, createdJson.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("First", createdJson.RootElement.GetProperty("title").GetString());
        Assert.Equal("2025-03-14T09:30:00Z", createdJson.RootElement.GetProperty("createdAt").GetString());

        var (_, got) = await SendAsync(client, HttpMethod.Get, "/api/notes/1");
        Assert.Equal(markdown, got.RootElement.GetProperty("content").GetString());

        var (patched, patchedJson) = await SendAsync(client, HttpMethod.Patch, "/api/notes/1", new UpdateNoteRequest { Content = "changed" });
        Assert.Equal(HttpStatusCode.OK, patched);
        Assert.Equal("First", patchedJson.RootElement.GetProperty("title").GetString());
        Assert.Equal("changed", patchedJson.RootElement.GetProperty("content").GetString());

        var (deleted, _) = await SendAsync(client, HttpMethod.Delete, "/api/notes/1");
        Assert.Equal(HttpStatusCode.NoContent, deleted);
        Assert.Empty(harness.Notes.Snapshot());
    }

    [Fact]
    public async Task ListIsNewestFirstAndSearchable()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        await SendAsync(client, HttpMethod.Post, "/api/notes", new CreateNoteRequest { Title = "alpha", Content = "one" });
        await SendAsync(client, HttpMethod.Post, "/api/notes", new CreateNoteRequest { Title = "beta", Content = "needle in body" });

        var (_, all) = await SendAsync(client, HttpMethod.Get, "/api/notes");
        var titles = all.RootElement.GetProperty("notes").EnumerateArray().Select(n => n.GetProperty("title").GetString());
        Assert.Equal(["beta", "alpha"], titles);

        var (_, found) = await SendAsync(client, HttpMethod.Get, "/api/notes?q=NEEDLE");
        Assert.Single(found.RootElement.GetProperty("notes").EnumerateArray());
    }

    [Fact]
    public async Task ValidationFailuresAre400WithFieldsAndMissingNotesAre404()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        var (status, json) = await SendAsync(client, HttpMethod.Post, "/api/notes", new CreateNoteRequest { Title = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(json.RootElement.GetProperty("fields").TryGetProperty("title", out _));

        var (tooLong, _) = await SendAsync(client, HttpMethod.Post, "/api/notes", new CreateNoteRequest { Title = "t", Content = new string('x', 50001) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong);

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/api/notes/9")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/api/notes/abc")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Patch, "/api/notes/9", new UpdateNoteRequest { Title = "x" })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, "/api/notes/9")).Status);
    }

    [Fact]
    public async Task MutationsStillRequireTheClientHeader()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post, "/api/notes", DashboardHarness.JsonBody(new CreateNoteRequest { Title = "x" }), withClientHeader: false);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
