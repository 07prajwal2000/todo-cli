using System.Net;
using Todo.Web.Http;

namespace Todo.Web.Tests;

public sealed class SpaceEndpointTests
{
    [Fact]
    public async Task ListReturnsTheDefaultSpaceAndTheConfiguredDefault()
    {
        await using var harness = await DashboardHarness.CreateAsync("work");
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/spaces");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.Equal("work", root.GetProperty("defaultSpace").GetString());
        Assert.Equal(["default"], Names(root).ToArray());
    }

    [Fact]
    public async Task CreateAddsASpaceAndReturns201()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/spaces",
            DashboardHarness.JsonBody(new Contracts.CreateSpaceRequest { Name = "personal" }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("personal", json.RootElement.GetProperty("name").GetString());

        using var listed = await client.GetAsync("/api/spaces");
        using var listedJson = await DashboardHarness.ReadJsonAsync(listed);
        Assert.Equal(["default", "personal"], Names(listedJson.RootElement).ToArray());
    }

    [Fact]
    public async Task CreateRejectsAnInvalidNameWithValidation()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/spaces",
            DashboardHarness.JsonBody(new Contracts.CreateSpaceRequest { Name = "_reserved" }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("validation", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.GetProperty("fields").TryGetProperty("space", out _));
    }

    [Fact]
    public async Task CreateReportsAConflictForADuplicate()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/spaces",
            DashboardHarness.JsonBody(new Contracts.CreateSpaceRequest { Name = "default" }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("conflict", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteRemovesASpaceAndReturns204()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        await harness.Spaces.CreateAsync(DashboardHarness.Space("temp"), CancellationToken.None);
        using var client = harness.CreateClient();

        using var response = await client.SendAsync(DashboardHarness.Request(HttpMethod.Delete, "/api/spaces/temp"))
            ;

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var listed = await client.GetAsync("/api/spaces");
        using var listedJson = await DashboardHarness.ReadJsonAsync(listed);
        Assert.Equal(["default"], Names(listedJson.RootElement).ToArray());
    }

    [Fact]
    public async Task DeleteRefusesTheDefaultSpace()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.SendAsync(DashboardHarness.Request(HttpMethod.Delete, "/api/spaces/default"))
            ;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("validation", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteOfAnUnknownSpaceReturns404()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.SendAsync(DashboardHarness.Request(HttpMethod.Delete, "/api/spaces/ghost"))
            ;

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("notfound", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteRequiresTheClientHeader()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client
            .SendAsync(DashboardHarness.Request(HttpMethod.Delete, "/api/spaces/default", withClientHeader: false))
            ;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void DefaultSpaceFallsBackWhenTheConfiguredNameIsInvalid()
    {
        var dependencies = new DashboardDependencies
        {
            TaskService = new Application.Tasks.TaskService(new InMemoryTaskRepository(), new FixedClock(DateTimeOffset.UnixEpoch)),
            SpaceService = new Application.Spaces.SpaceService(new InMemorySpaceRepository()),
            Clock = new FixedClock(DateTimeOffset.UnixEpoch),
            DefaultSpace = "   ",
        };

        Assert.Equal(Todo.Domain.Spaces.SpaceName.DefaultValue, DashboardEndpoints.DefaultSpaceOf(dependencies).Value);
    }

    private static IEnumerable<string> Names(System.Text.Json.JsonElement root) =>
        root.GetProperty("spaces").EnumerateArray().Select(space => space.GetString()!);
}
