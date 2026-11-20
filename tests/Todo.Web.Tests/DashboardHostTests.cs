using System.Net;

namespace Todo.Web.Tests;

public sealed class DashboardHostTests
{
    [Fact]
    public void BuildUrlPinsLoopbackAndTheGivenPort()
    {
        Assert.Equal("http://127.0.0.1:12000", DashboardHost.BuildUrl(12000));
        Assert.Equal("http://127.0.0.1:4321", DashboardHost.BuildUrl(4321));
        Assert.DoesNotContain("0.0.0.0", DashboardHost.BuildUrl(12000), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssetsAreServedOverHttpWithTheMappedContentTypes()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        using var index = await client.GetAsync("/");
        using var css = await client.GetAsync("/app.css");
        using var js = await client.GetAsync("/app.js");
        using var bootstrap = await client.GetAsync("/bootstrap.min.css");
        using var font = await client.GetAsync("/inter-latin.woff2");

        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal("text/html", index.Content.Headers.ContentType?.MediaType);
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
        Assert.Equal("text/javascript", js.Content.Headers.ContentType?.MediaType);
        Assert.Equal("text/css", bootstrap.Content.Headers.ContentType?.MediaType);
        Assert.Equal("font/woff2", font.Content.Headers.ContentType?.MediaType);

        Assert.NotNull(index.Headers.CacheControl);
        Assert.True(index.Headers.CacheControl!.NoCache);
    }

    [Fact]
    public async Task BootstrapIsVendoredAndNotReferencedFromACdn()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.DoesNotContain("cdn.", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/bootstrap.min.css", html, StringComparison.Ordinal);

        var css = await client.GetStringAsync("/bootstrap.min.css");

        Assert.DoesNotContain("fonts.googleapis.com", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SameOriginMutatingRequestIsAccepted()
    {
        await using var harness = await DashboardHarness.CreateAsync();
        using var client = harness.CreateClient();
        var origin = new Uri(harness.App.Urls.First()).GetLeftPart(UriPartial.Authority);

        using var request = DashboardHarness.Request(
            HttpMethod.Post,
            "/api/tasks",
            DashboardHarness.JsonBody(new Contracts.CreateTaskRequest { Name = "Same origin" }),
            origin: origin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RunAsyncCompletesWhenTheCancellationTokenIsSignalled()
    {
        var clock = new FixedClock(DashboardHarness.Now);
        var dependencies = new DashboardDependencies
        {
            TaskService = new Application.Tasks.TaskService(new InMemoryTaskRepository(), clock),
            NoteService = new Application.Notes.NoteService(new InMemoryNoteRepository(), clock),
            SpaceService = new Application.Spaces.SpaceService(new InMemorySpaceRepository()),
            Clock = clock,
        };

        var port = FreePort();
        var options = new DashboardOptions { Port = port, OpenBrowser = false };
        using var cancellation = new CancellationTokenSource();

        var running = DashboardHost.RunAsync(dependencies, options, cancellation.Token);

        await WaitForServerAsync(port).ConfigureAwait(true);
        async Task Call() => await cancellation.CancelAsync().ConfigureAwait(true);
        await Call().ConfigureAwait(true);

        var finished = await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(true);
        Assert.Same(running, finished);
        await running.ConfigureAwait(true);

        Assert.Equal($"http://127.0.0.1:{port}", DashboardHost.BuildUrl(port));
    }

    [Fact]
    public async Task PortInUseSurfacesAsAFailureInsteadOfHanging()
    {
        var clock = new FixedClock(DashboardHarness.Now);
        var dependencies = new DashboardDependencies
        {
            TaskService = new Application.Tasks.TaskService(new InMemoryTaskRepository(), clock),
            NoteService = new Application.Notes.NoteService(new InMemoryNoteRepository(), clock),
            SpaceService = new Application.Spaces.SpaceService(new InMemorySpaceRepository()),
            Clock = clock,
        };

        var port = FreePort();

        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
        listener.Start();

        var options = new DashboardOptions { Port = port, OpenBrowser = false };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            DashboardHost.RunAsync(dependencies, options, CancellationToken.None)).ConfigureAwait(true);

        listener.Stop();
    }

    internal static int FreePort()
    {
        using var socket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static async Task WaitForServerAsync(int port)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/api/health")).ConfigureAwait(true);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(50).ConfigureAwait(true);
        }

        throw new InvalidOperationException($"The dashboard never came up on port {port}.");
    }
}
