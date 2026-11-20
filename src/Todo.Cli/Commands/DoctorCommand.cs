using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Setup;
using Todo.Cli.Infrastructure;
using Todo.Domain.Tasks;

namespace Todo.Cli.Commands;

public sealed class DoctorCommand(DiagnosticsService diagnostics, ConsoleReporter reporter)
    : AsyncCommand<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--fix")]
        [Description("Recreate missing tabs and headers when the schema is not ready.")]
        public bool Fix { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var report = await diagnostics.RunAsync(cancellationToken).ConfigureAwait(false);
        if (!report.TryGetValue(out var result))
        {
            reporter.ReportError(report.Error!);
            AnsiConsole.MarkupLine("[grey]Run 'todo init' if this is a fresh machine or the credentials were removed.[/]");
            return ExitCodes.Configuration;
        }

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        grid.AddRow("[grey]settings[/]", Markup.Escape(result.SettingsLocation));
        grid.AddRow("[grey]credentials[/]", Markup.Escape(result.CredentialsLocation));
        grid.AddRow("[grey]spreadsheet[/]", Markup.Escape(result.SpreadsheetTitle));
        grid.AddRow("[grey]spreadsheet id[/]", Markup.Escape(result.Settings.SpreadsheetId));
        grid.AddRow("[grey]default space[/]", Markup.Escape(result.Settings.DefaultSpace));
        grid.AddRow("[grey]service account[/]", Markup.Escape(result.ServiceAccountEmail ?? "<unknown>"));
        grid.AddRow("[grey]tabs[/]", Markup.Escape(string.Join(", ", result.Tabs)));
        grid.AddRow("[grey]schema version[/]", result.SchemaVersion.ToString());

        var schemaLine = result.SchemaReady
            ? $"[green]ready[/] (v{TaskSchema.Version})"
            : $"[red]not ready[/] (found v{result.SchemaVersion}, expected v{TaskSchema.Version})";

        grid.AddRow("[grey]schema[/]", schemaLine);

        AnsiConsole.Write(grid);

        if (result.SchemaReady)
        {
            reporter.Success("Everything looks healthy.");
            return ExitCodes.Success;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]The sheet does not match the expected schema.[/]");

        if (!settings.Fix)
        {
            AnsiConsole.MarkupLine("[grey]Re-run with --fix to create the missing tabs and headers.[/]");
            return ExitCodes.Failure;
        }

        var repaired = await diagnostics.RepairAsync(cancellationToken).ConfigureAwait(false);
        if (!repaired.TryGetValue(out var repair))
        {
            return reporter.ReportError(repaired.Error!);
        }

        AnsiConsole.WriteLine();

        foreach (var action in repair.Actions)
        {
            reporter.Info($"  • {action}");
        }

        var after = await diagnostics.RunAsync(cancellationToken).ConfigureAwait(false);

        if (after.TryGetValue(out var healed) && healed.SchemaReady)
        {
            reporter.Success("Schema repaired.");
            return ExitCodes.Success;
        }

        reporter.Warn("Some columns are still out of place. Fix the header row by hand: " +
            "existing data is never moved automatically.");

        return ExitCodes.Failure;
    }
}
