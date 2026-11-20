using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Common;
using Todo.Application.Setup;
using Todo.Application.Spaces;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Web;

namespace Todo.Cli.Commands;

public sealed class ServerCommand(
    TaskService tasks,
    SpaceService spaces,
    ISettingsStore settingsStore,
    IClock clock,
    SpaceResolver resolver,
    ConsoleReporter reporter) : AsyncCommand<ServerCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandOption("-p|--port <PORT>")]
        [Description("Port to listen on (loopback only). Defaults to 12000.")]
        public int? Port { get; init; }

        [CommandOption("--no-open")]
        [Description("Do not open the dashboard in a browser.")]
        public bool NoOpen { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Port is { } requested && (requested is < 1 or > 65535))
        {
            return reporter.ReportError(TodoError.Validation(
                $"Port {requested} is out of range. Pick a port between 1 and 65535."));
        }

        var configured = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!configured.TryGetValue(out var config))
        {
            return reporter.ReportError(configured.Error!);
        }

        var space = await resolver.ResolveAsync(settings.Space, cancellationToken).ConfigureAwait(false);
        if (!space.TryGetValue(out var initialSpace))
        {
            return reporter.ReportError(space.Error!);
        }

        var port = settings.Port ?? config.ServerPort;

        var dependencies = new DashboardDependencies
        {
            TaskService = tasks,
            SpaceService = spaces,
            Clock = clock,
            DefaultSpace = initialSpace.Value,
            Provider = config.Provider,
            SpreadsheetId = config.SpreadsheetId,
        };

        var url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}");

        AnsiConsole.MarkupLine($"[green]Dashboard[/] {Markup.Escape(url)} [grey](Ctrl+C to stop)[/]");
        AnsiConsole.MarkupLine($"[grey]Space: {Markup.Escape(initialSpace.Value)}[/]");

        try
        {
            await DashboardHost.RunAsync(
                dependencies,
                new DashboardOptions { Port = port, OpenBrowser = !settings.NoOpen },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[grey]Dashboard stopped.[/]");
            return ExitCodes.Success;
        }
        catch (IOException exception)
        {
            return reporter.ReportError(TodoError.Unavailable(
                $"Port {port} could not be opened: {exception.Message} Try -p <other port>."));
        }

        return ExitCodes.Success;
    }
}
