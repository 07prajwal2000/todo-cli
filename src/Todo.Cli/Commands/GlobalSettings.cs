using System.ComponentModel;
using Spectre.Console.Cli;

namespace Todo.Cli.Commands;

/// <summary>
/// Options every command shares. Declared once so <c>-s</c>, <c>--json</c> and <c>-v</c> behave
/// identically everywhere instead of drifting per command.
/// </summary>
public class SpaceSettings : CommandSettings
{
    [CommandOption("-s|--space <SPACE>")]
    [Description("Space (sheet tab) to act on. Defaults to $TODO_SPACE, then the configured default.")]
    public string? Space { get; init; }

    [CommandOption("--json")]
    [Description("Emit machine-readable JSON instead of a table.")]
    public bool Json { get; init; }
}

public class GlobalSettings : SpaceSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Show verbose diagnostics.")]
    public bool Verbose { get; init; }

    [CommandOption("--no-color")]
    [Description("Disable ANSI colour output.")]
    public bool NoColor { get; init; }
}
