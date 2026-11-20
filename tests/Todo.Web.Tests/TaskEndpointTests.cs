using System.Net;
using System.Text.Json;
using Todo.Domain.Tasks;

namespace Todo.Web.Tests;

public sealed class TaskEndpointTests
{
    [Fact]
    public async Task PostCreatesATaskAndReturns201WithTheDto()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            DashboardHarness.JsonBody(new Contracts.CreateTaskRequest
            {
                Name = "Ship the dashboard",
                Description = "Kestrel + vanilla UI",
                DueOn = "2025-03-20",
                Status = "ongoing",
                Remarks = "looks good",
            }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.Equal(1, root.GetProperty("id").GetInt32());
        Assert.Equal("Ship the dashboard", root.GetProperty("name").GetString());
        Assert.Equal("Kestrel + vanilla UI", root.GetProperty("description").GetString());
        Assert.Equal("ongoing", root.GetProperty("status").GetString());
        Assert.Equal("2025-03-20", root.GetProperty("dueOn").GetString());
        Assert.Equal("looks good", root.GetProperty("remarks").GetString());
        Assert.Equal("2025-03-14T09:30:00Z", root.GetProperty("createdAt").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("completedAt").ValueKind);

        Assert.Single(harness.Tasks.Snapshot(Todo.Domain.Spaces.SpaceName.Default));
    }

    [Fact]
    public async Task PostAcceptsStatusAliasesAndDefaultsToPending()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var aliasRequest = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            DashboardHarness.JsonBody(new Contracts.CreateTaskRequest { Name = "Alias", Status = "wip" }));
        using var aliasResponse = await client.SendAsync(aliasRequest);

        using var defaultRequest = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            DashboardHarness.JsonBody(new Contracts.CreateTaskRequest { Name = "Plain" }));
        using var defaultResponse = await client.SendAsync(defaultRequest);

        using var aliasJson = await DashboardHarness.ReadJsonAsync(aliasResponse);
        using var defaultJson = await DashboardHarness.ReadJsonAsync(defaultResponse);

        Assert.Equal("ongoing", aliasJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("pending", defaultJson.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostReportsValidationFailureWithFields()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            DashboardHarness.JsonBody(new Contracts.CreateTaskRequest { Name = "   " }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("validation", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.GetProperty("fields").TryGetProperty("name", out _));
        Assert.Empty(harness.Tasks.Snapshot(Todo.Domain.Spaces.SpaceName.Default));
    }

    [Fact]
    public async Task GetByIdReturnsTheTask()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(Todo.Domain.Spaces.SpaceName.Default, DashboardHarness.Task(5, "Seeded"));
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks/5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("Seeded", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetByIdReturns404WithCodeNotFoundForUnknownIds()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("notfound", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetByNonNumericIdReturns404()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListReturnsTasksForTheSpace()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(
            Todo.Domain.Spaces.SpaceName.Default,
            DashboardHarness.Task(1, "First", TaskState.Pending),
            DashboardHarness.Task(2, "Second", TaskState.Ongoing));
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks?status=all");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("default", json.RootElement.GetProperty("space").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("tasks").GetArrayLength());
    }

    [Fact]
    public async Task ListFiltersByCommaSeparatedStatusesAndAliases()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(
            Todo.Domain.Spaces.SpaceName.Default,
            DashboardHarness.Task(1, "First", TaskState.Pending),
            DashboardHarness.Task(2, "Second", TaskState.Ongoing),
            DashboardHarness.Task(3, "Third", TaskState.Completed),
            DashboardHarness.Task(4, "Fourth", TaskState.Backlog));
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks?status=done,started");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        var tasks = json.RootElement.GetProperty("tasks");

        Assert.Equal(2, tasks.GetArrayLength());
        // Default ordering puts active work first, so ongoing (#2) precedes completed (#3).
        Assert.Equal([2, 3], Ids(tasks));
    }

    [Fact]
    public async Task ListWithoutAStatusDefaultsToOpenWork()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(
            Todo.Domain.Spaces.SpaceName.Default,
            DashboardHarness.Task(1, "First", TaskState.Pending),
            DashboardHarness.Task(2, "Second", TaskState.Completed),
            DashboardHarness.Task(3, "Third", TaskState.Backlog));
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks");

        using var json = await DashboardHarness.ReadJsonAsync(response);
        var tasks = json.RootElement.GetProperty("tasks");

        Assert.Equal(1, tasks.GetArrayLength());
        Assert.Equal("First", tasks[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListRejectsAnUnknownStatus()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks?status=nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("validation", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ListAppliesTheSearchTerm()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(
            Todo.Domain.Spaces.SpaceName.Default,
            DashboardHarness.Task(1, "Write docs", TaskState.Pending, remarks: "about needles"),
            DashboardHarness.Task(2, "Buy milk", TaskState.Pending));
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks?status=all&q=needles");

        using var json = await DashboardHarness.ReadJsonAsync(response);
        var tasks = json.RootElement.GetProperty("tasks");

        Assert.Equal(1, tasks.GetArrayLength());
        Assert.Equal(1, tasks[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task PatchEditsFields()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(
            Todo.Domain.Spaces.SpaceName.Default,
            DashboardHarness.Task(3, "Old name", TaskState.Pending, dueOn: "2025-04-01"));
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Patch,
            "/api/tasks/3",
            DashboardHarness.JsonBody(new Contracts.UpdateTaskRequest
            {
                Name = "New name",
                Description = "now described",
                ClearDue = true,
            }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("New name", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("now described", json.RootElement.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("dueOn").ValueKind);
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PatchWithStatusAndFieldsAppliesTheEditThenTheStateChange()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(Todo.Domain.Spaces.SpaceName.Default, DashboardHarness.Task(4, "Original", TaskState.Pending));
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Patch,
            "/api/tasks/4",
            DashboardHarness.JsonBody(new Contracts.UpdateTaskRequest { Name = "Renamed", Status = "completed" }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.Equal("Renamed", root.GetProperty("name").GetString());
        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal("2025-03-14T09:30:00Z", root.GetProperty("completedAt").GetString());

        var stored = harness.Tasks.Snapshot(Todo.Domain.Spaces.SpaceName.Default).Single();
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal(TaskState.Completed, stored.Status);
    }

    [Fact]
    public async Task PatchStatusOnlyChangesState()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(Todo.Domain.Spaces.SpaceName.Default, DashboardHarness.Task(6, "Task", TaskState.Pending));
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Patch,
            "/api/tasks/6",
            DashboardHarness.JsonBody(new Contracts.UpdateTaskRequest { Status = "ongoing" }));

        using var response = await client.SendAsync(request);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("ongoing", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Task", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PatchReportsAnIllegalTransitionAsValidation()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(Todo.Domain.Spaces.SpaceName.Default, DashboardHarness.Task(7, "Task", TaskState.Completed));
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Patch,
            "/api/tasks/7",
            DashboardHarness.JsonBody(new Contracts.UpdateTaskRequest { Status = "backlog" }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("validation", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchUnknownIdReturns404()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Patch,
            "/api/tasks/999",
            DashboardHarness.JsonBody(new Contracts.UpdateTaskRequest { Name = "x" }));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("notfound", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteRemovesTheTaskAndReturns204()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(Todo.Domain.Spaces.SpaceName.Default, DashboardHarness.Task(8, "Doomed"));
        using var client = harness.CreateClient();

        using var response = await client.SendAsync(DashboardHarness.Request(HttpMethod.Delete, "/api/tasks/8"))
            ;

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(harness.Tasks.Snapshot(Todo.Domain.Spaces.SpaceName.Default));
    }

    [Fact]
    public async Task DeleteUnknownIdReturns404()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.SendAsync(DashboardHarness.Request(HttpMethod.Delete, "/api/tasks/42"))
            ;

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("notfound", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task MutatingRequestsWithoutTheClientHeaderAreRejected()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            DashboardHarness.JsonBody(new Contracts.CreateTaskRequest { Name = "Blocked" }),
            withClientHeader: false);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, harness.Tasks.AddCalls);
    }

    [Fact]
    public async Task CrossOriginMutatingRequestIsRejected()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            DashboardHarness.JsonBody(new Contracts.CreateTaskRequest { Name = "Blocked" }),
            origin: "https://evil.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonJsonContentTypeIsRejected()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            new StringContent("name=blocked", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReadRequestsDoNotNeedTheClientHeader()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TasksHonourTheSpaceQueryParameter()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        harness.Tasks.Seed(DashboardHarness.Space("work"), DashboardHarness.Task(1, "Work item"));
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/tasks?space=work&status=all");

        using var json = await DashboardHarness.ReadJsonAsync(response);
        Assert.Equal("work", json.RootElement.GetProperty("space").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("tasks").GetArrayLength());
    }

    [Fact]
    public async Task HealthReportsTheConfiguration()
    {
        await using var harness = await DashboardHarness.CreateAsync("work");
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await DashboardHarness.ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("in-memory", root.GetProperty("provider").GetString());
        Assert.Equal("work", root.GetProperty("defaultSpace").GetString());
        Assert.Equal("test-sheet", root.GetProperty("spreadsheetId").GetString());
        Assert.Equal("2025-03-14T09:30:00Z", root.GetProperty("serverTime").GetString());
    }

    [Fact]
    public async Task RootServesTheSinglePageApp()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Todo Dashboard", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownAssetPathReturns404()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var response = await client.GetAsync("/missing.css");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static int[] Ids(JsonElement tasks) =>
        [.. tasks.EnumerateArray().Select(task => task.GetProperty("id").GetInt32())];
}
