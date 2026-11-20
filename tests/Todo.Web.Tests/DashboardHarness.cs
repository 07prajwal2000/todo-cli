using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Todo.Application.Notes;
using Todo.Application.Spaces;
using Todo.Application.Tasks;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;
using Todo.Web.Contracts;

namespace Todo.Web.Tests;

/// <summary>
/// Builds a dashboard application over the in-memory doubles. The same wiring the CLI gets is used,
/// only the persistence ports are swapped.
/// </summary>
internal sealed class DashboardHarness : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2025, 3, 14, 9, 30, 0, TimeSpan.Zero);

    private DashboardHarness(
        WebApplication app,
        InMemoryTaskRepository tasks,
        InMemorySpaceRepository spaces,
        InMemoryNoteRepository notes)
    {
        App = app;
        Tasks = tasks;
        Spaces = spaces;
        Notes = notes;
    }

    public InMemoryNoteRepository Notes { get; }

    public WebApplication App { get; }

    public InMemoryTaskRepository Tasks { get; }

    public InMemorySpaceRepository Spaces { get; }

    public static DashboardHarness Create(string? defaultSpace = null) =>
        CreateAsync(defaultSpace).GetAwaiter().GetResult();

    public static async Task<DashboardHarness> CreateAsync(string? defaultSpace = null)
    {
        var tasks = new InMemoryTaskRepository();
        var spaces = new InMemorySpaceRepository();
        var notes = new InMemoryNoteRepository();
        var clock = new FixedClock(Now);

        var dependencies = new DashboardDependencies
        {
            TaskService = new TaskService(tasks, clock),
            NoteService = new NoteService(notes, clock),
            SpaceService = new SpaceService(spaces),
            Clock = clock,
            DefaultSpace = defaultSpace,
            Provider = "in-memory",
            SpreadsheetId = "test-sheet",
        };

        var options = new DashboardOptions { Port = 0, OpenBrowser = false };
        var app = DashboardHost.BuildApp(dependencies, options);

        app.Urls.Clear();
        await app.StartAsync();

        return new DashboardHarness(app, tasks, spaces, notes);
    }

    public HttpClient CreateClient() => new()
    {
        BaseAddress = new Uri(App.Urls.First()),
    };

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
    }

    public static StringContent JsonBody<T>(T value) =>
        new(JsonSerializer.Serialize(value, DashboardJsonContext.Default.Options), Encoding.UTF8, "application/json");

    public static HttpRequestMessage Request(
        HttpMethod method,
        string url,
        HttpContent? body = null,
        bool withClientHeader = true,
        string? origin = null,
        string? contentType = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = body;

            if (contentType is not null)
            {
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            }
        }

        if (withClientHeader)
        {
            request.Headers.Add("X-Todo-Client", "dashboard");
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return request;
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }

    public static TaskItem Task(
        int id,
        string name,
        TaskState state = TaskState.Pending,
        string? dueOn = null,
        string description = "",
        string remarks = "",
        DateTimeOffset? completedAt = null) =>
        new()
        {
            Id = new TaskId(id),
            Name = name,
            Description = description,
            Status = state,
            DueOn = dueOn is null ? null : DueDate.From(DateOnly.ParseExact(dueOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            CreatedAt = Now,
            CompletedAt = completedAt,
            Remarks = remarks,
        };

    public static SpaceName Space(string name) => SpaceName.Create(name);
}
