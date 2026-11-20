using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Setup;
using Todo.Cli.Infrastructure;
using Todo.Infrastructure.Configuration;

namespace Todo.Cli.Commands;

public sealed class InitCommand(
    SetupService setup,
    ISettingsStore settingsStore,
    ConfigPaths paths,
    ConsoleReporter reporter,
    IAnsiConsole console) : AsyncCommand<InitCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--force")]
        [Description("Overwrite an existing configuration.")]
        public bool Force { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var exists = await settingsStore.ExistsAsync(cancellationToken).ConfigureAwait(false);
        if (exists.TryGetValue(out var configured) && configured && !settings.Force)
        {
            reporter.Warn($"A configuration already exists at {paths.SettingsFilePath}.");
            reporter.Info("Re-run with --force to replace it.");
            return ExitCodes.Failure;
        }

        console.Write(new Rule("[bold]todo init[/]").LeftJustified());
        console.MarkupLine("[grey]Answers are stored only in your personal config directory.[/]");
        console.MarkupLine($"[grey]Location: {Markup.Escape(paths.ConfigDirectory)}[/]");
        console.WriteLine();

        var credentialsPath = console.Prompt(
            new TextPrompt<string>("Path to your Google [bold]service account key[/] (JSON):")
                .Validate(path =>
                {
                    if (!File.Exists(path))
                    {
                        return ValidationResult.Error("File not found.");
                    }

                    var raw = File.ReadAllText(path);
                    return ServiceAccountKey.TryParse(raw, out _, out var error)
                        ? ValidationResult.Success()
                        : ValidationResult.Error(error!);
                }));

        var credentialsJson = await File.ReadAllTextAsync(credentialsPath, cancellationToken).ConfigureAwait(false);
        if (!ServiceAccountKey.TryParse(credentialsJson, out var key, out var keyError))
        {
            return reporter.ReportError(Todo.Application.Common.TodoError.Validation(keyError!));
        }

        var spreadsheet = console.Prompt(
            new TextPrompt<string>("Spreadsheet ID or URL:")
                .Validate(input =>
                {
                    var id = SpreadsheetIdParser.TryExtract(input, out var error);
                    return id is null ? ValidationResult.Error(error!) : ValidationResult.Success();
                }));

        var spreadsheetId = SpreadsheetIdParser.TryExtract(spreadsheet, out _)!;

        var defaultSpace = console.Prompt(
            new TextPrompt<string>("Default space (sheet tab):")
                .DefaultValue("default"));

        console.WriteLine();

        var initialized = await setup.InitializeAsync(
            new InitRequest
            {
                CredentialsJson = credentialsJson,
                SpreadsheetId = spreadsheetId,
                DefaultSpace = defaultSpace,
                ServiceAccountEmail = key?.ClientEmail ?? string.Empty,
            },
            cancellationToken).ConfigureAwait(false);

        if (initialized.IsFailure)
        {
            return reporter.ReportError(initialized.Error!);
        }

        reporter.Success("Configuration written and the sheet is ready.");

        console.WriteLine();
        console.MarkupLine("[bold]Next:[/] share the spreadsheet with this address as [bold]Editor[/]:");
        console.MarkupLine($"  [blue]{Markup.Escape(key?.ClientEmail ?? "<client_email>")}[/]");
        console.MarkupLine($"  [grey]https://docs.google.com/spreadsheets/d/{spreadsheetId}[/]");
        console.WriteLine();
        console.MarkupLine($"[grey]Config: {Markup.Escape(paths.SettingsFilePath)}[/]");
        console.MarkupLine("[grey]Try: todo add \"my first task\" --due 2026-12-01[/]");

        return ExitCodes.Success;
    }
}

/// <summary>
/// Users paste either a bare id or a full Sheets URL, so both are accepted and the extraction rule
/// lives here rather than being re-derived by each caller.
/// </summary>
public static class SpreadsheetIdParser
{
    public static string? TryExtract(string? input, out string? error) =>
        SpreadsheetId.TryExtract(input, out error);
}
