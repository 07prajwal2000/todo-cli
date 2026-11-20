using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Setup;
using Todo.Application.Spaces;
using Todo.Cli.Infrastructure;

namespace Todo.Cli.Commands;

public sealed class SpacesCommand(SpaceService spaces, ISettingsStore settingsStore, ConsoleReporter reporter)
    : AsyncCommand<SpacesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Emit machine-readable JSON.")]
        public bool Json { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var listed = await spaces.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!listed.TryGetValue(out var names))
        {
            return reporter.ReportError(listed.Error!);
        }

        var configured = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!configured.TryGetValue(out var config))
        {
            return reporter.ReportError(configured.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Spaces(names, config.DefaultSpace));
            return ExitCodes.Success;
        }

        if (names.Count == 0)
        {
            reporter.Info("No spaces yet. Run 'todo init' or 'todo space add <name>'.");
            return ExitCodes.Success;
        }

        foreach (var name in names)
        {
            var marker = string.Equals(name.Value, config.DefaultSpace, StringComparison.OrdinalIgnoreCase)
                ? " [grey](default)[/]"
                : string.Empty;

            AnsiConsole.MarkupLine($"  {Markup.Escape(name.Value)}{marker}");
        }

        return ExitCodes.Success;
    }
}

public sealed class SpaceAddCommand(SpaceService spaces, ConsoleReporter reporter)
    : AsyncCommand<SpaceAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("New space name (becomes a sheet tab).")]
        public required string Name { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var created = await spaces.CreateAsync(settings.Name, cancellationToken).ConfigureAwait(false);
        if (!created.TryGetValue(out var space))
        {
            return reporter.ReportError(created.Error!);
        }

        reporter.Success($"Created space '{space.Value}'.");
        return ExitCodes.Success;
    }
}

public sealed class SpaceRemoveCommand(SpaceService spaces, ConsoleReporter reporter, IAnsiConsole console)
    : AsyncCommand<SpaceRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Space to delete (its sheet tab and all its tasks).")]
        public required string Name { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip the confirmation prompt.")]
        public bool Yes { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!settings.Yes)
        {
            var confirmed = console.Prompt(
                new ConfirmationPrompt($"Delete space '{settings.Name}' and every task in it?")
                {
                    DefaultValue = false,
                });

            if (!confirmed)
            {
                reporter.Info("Cancelled.");
                return ExitCodes.Success;
            }
        }

        var deleted = await spaces.DeleteAsync(settings.Name, cancellationToken).ConfigureAwait(false);
        if (deleted.IsFailure)
        {
            return reporter.ReportError(deleted.Error!);
        }

        reporter.Success($"Deleted space '{settings.Name}'.");
        return ExitCodes.Success;
    }
}
