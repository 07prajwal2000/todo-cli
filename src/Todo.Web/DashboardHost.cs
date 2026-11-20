using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Todo.Web.Assets;
using Todo.Web.Http;

namespace Todo.Web;

/// <summary>
/// Entry point for the dashboard server. The CLI owns the console, so the host is created with no
/// logging providers and no welcome/hosting banners: the CLI prints the one URL line the user needs.
/// </summary>
public static class DashboardHost
{
    public static Task RunAsync(
        DashboardDependencies dependencies,
        DashboardOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(options);

        return RunCoreAsync(dependencies, options, cancellationToken);
    }

    internal static string BuildUrl(int port) => $"http://127.0.0.1:{port}";

    /// <summary>
    /// Builds the application without running it. Exposed to the test assembly so endpoint behaviour
    /// can be exercised against the same wiring the CLI gets.
    /// </summary>
    internal static WebApplication BuildApp(DashboardDependencies dependencies, DashboardOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production });

        builder.Logging.ClearProviders();
        // Loopback only, never 0.0.0.0: this server has no authentication and the CLI runs it locally.
        builder.WebHost.UseUrls(BuildUrl(options.Port));

        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            json.SerializerOptions.TypeInfoResolverChain.Insert(0, Contracts.DashboardJsonContext.Default);
        });

        var app = builder.Build();
        var assets = new EmbeddedAssetProvider();

        app.UseMiddleware<LocalRequestGuardMiddleware>();
        app.MapDashboardApi(dependencies);
        app.MapDashboardAssets(assets);

        return app;
    }

    private static async Task RunCoreAsync(
        DashboardDependencies dependencies,
        DashboardOptions options,
        CancellationToken cancellationToken)
    {
        var app = BuildApp(dependencies, options);

        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        if (options.OpenBrowser)
        {
            OpenBrowser(BuildUrl(options.Port));
        }

        try
        {
            await app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Best effort only: a headless machine or a locked-down shell session has no browser and that
    /// must never take the server down with it.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Launching a browser is a convenience; any failure is ignored by design.")]
    private static void OpenBrowser(string url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Ignored on purpose, see the summary.
        }
    }
}
