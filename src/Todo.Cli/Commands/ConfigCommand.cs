using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Common;
using Todo.Application.Setup;
using Todo.Cli.Infrastructure;
using Todo.Infrastructure.Configuration;

namespace Todo.Cli.Commands;

public sealed class ConfigCommand(ISettingsStore store, ConfigPaths paths, ConsoleReporter reporter)
    : AsyncCommand<ConfigCommand.Settings>
{
    private static readonly System.Text.Json.JsonSerializerOptions DisplayJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ACTION>")]
        [Description("One of: path, show, set.")]
        public required string Action { get; init; }

        [CommandArgument(1, "[KEY]")]
        [Description("Setting key for 'set': spreadsheetId, defaultSpace, serverPort, provider.")]
        public string? Key { get; init; }

        [CommandArgument(2, "[VALUE]")]
        [Description("New value for 'set'.")]
        public string? Value { get; init; }

        [CommandOption("--json")]
        [Description("Emit machine-readable JSON (show only).")]
        public bool Json { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        settings.Action.Trim().ToLowerInvariant() switch
        {
            "path" => ShowPath(),
            "show" => await ShowAsync(settings.Json, cancellationToken).ConfigureAwait(false),
            "set" => await SetAsync(settings.Key, settings.Value, cancellationToken).ConfigureAwait(false),
            _ => reporter.ReportError(TodoError.Validation(
                $"'{settings.Action}' is not a config action. Use path, show or set.")),
        };

    private int ShowPath()
    {
        AnsiConsole.MarkupLine(Markup.Escape(paths.SettingsFilePath));
        AnsiConsole.MarkupLine($"[grey]config dir: {Markup.Escape(paths.ConfigDirectory)}[/]");
        return ExitCodes.Success;
    }

    private async Task<int> ShowAsync(bool json, CancellationToken cancellationToken)
    {
        var loaded = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!loaded.TryGetValue(out var config))
        {
            return reporter.ReportError(loaded.Error!);
        }

        if (json)
        {
            AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(config, DisplayJsonOptions));

            return ExitCodes.Success;
        }

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        grid.AddRow("[grey]provider[/]", Markup.Escape(config.Provider));
        grid.AddRow("[grey]spreadsheetId[/]", Markup.Escape(config.SpreadsheetId));
        grid.AddRow("[grey]defaultSpace[/]", Markup.Escape(config.DefaultSpace));
        grid.AddRow("[grey]credentialsFile[/]", Markup.Escape(config.CredentialsFile));
        grid.AddRow("[grey]serverPort[/]", config.ServerPort.ToString(CultureInfo.InvariantCulture));
        grid.AddRow("[grey]location[/]", Markup.Escape(store.DescribeLocation()));

        AnsiConsole.Write(grid);
        return ExitCodes.Success;
    }

    private async Task<int> SetAsync(string? key, string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || value is null)
        {
            return reporter.ReportError(TodoError.Validation("'config set' needs a key and a value."));
        }

        var loaded = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!loaded.TryGetValue(out var config))
        {
            return reporter.ReportError(loaded.Error!);
        }

        TodoSettings updated;

        switch (key.Trim().ToLowerInvariant())
        {
            case "spreadsheetid":
            case "spreadsheet":
                var id = SpreadsheetIdParser.TryExtract(value, out var idError);
                if (id is null)
                {
                    return reporter.ReportError(TodoError.Validation(idError!));
                }

                updated = config with { SpreadsheetId = id };
                break;

            case "defaultspace":
            case "space":
                if (Todo.Domain.Spaces.SpaceName.TryCreate(value) is null)
                {
                    Todo.Domain.Spaces.SpaceName.IsValid(value, out var spaceError);
                    return reporter.ReportError(TodoError.Validation(spaceError!));
                }

                updated = config with { DefaultSpace = value.Trim() };
                break;

            case "serverport":
            case "port":
                if (!int.TryParse(value, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
                {
                    return reporter.ReportError(TodoError.Validation(
                        $"'{value}' is not a valid port. Pick a number between 1 and 65535."));
                }

                updated = config with { ServerPort = port };
                break;

            case "provider":
                if (!string.Equals(value.Trim(), ProviderNames.GoogleSheets, StringComparison.OrdinalIgnoreCase))
                {
                    return reporter.ReportError(TodoError.Validation(
                        $"Only '{ProviderNames.GoogleSheets}' is supported in this version."));
                }

                updated = config with { Provider = ProviderNames.GoogleSheets };
                break;

            default:
                return reporter.ReportError(TodoError.Validation(
                    $"'{key}' is not a known setting. Use spreadsheetId, defaultSpace, serverPort or provider."));
        }

        var saved = await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return reporter.ReportError(saved.Error!);
        }

        reporter.Success($"Set {key}.");
        return ExitCodes.Success;
    }
}
