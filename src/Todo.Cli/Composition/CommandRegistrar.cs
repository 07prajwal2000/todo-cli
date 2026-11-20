using Spectre.Console.Cli;
using Todo.Cli.Commands;

namespace Todo.Cli.Composition;

/// <summary>
/// Declares the command surface in one place. Adding a command is a single line here plus the
/// command type itself.
/// </summary>
public static class CommandRegistrar
{
    public static void Register(IConfigurator config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.SetApplicationName("todo");
        config.SetApplicationVersion("0.1.0");

        config.AddCommand<InitCommand>("init")
            .WithDescription("Configure credentials and spreadsheet, then prepare the sheet.");

        config.AddCommand<AddCommand>("add")
            .WithDescription("Add a task to a space.");

        config.AddCommand<ListCommand>("ls")
            .WithDescription("List tasks (alias: list).");

        config.AddCommand<ListCommand>("list")
            .WithDescription("List tasks.")
            .IsHidden();

        config.AddCommand<ShowCommand>("show")
            .WithDescription("Show one task in detail.");

        config.AddCommand<EditCommand>("edit")
            .WithDescription("Edit a task's fields.");

        config.AddCommand<StateCommand>("status")
            .WithDescription("Move a task to a specific state.");

        config.AddCommand<DoneCommand>("done")
            .WithDescription("Shortcut for 'status <id> completed'.");

        config.AddCommand<StartCommand>("start")
            .WithDescription("Shortcut for 'status <id> ongoing'.");

        config.AddCommand<ReopenCommand>("reopen")
            .WithDescription("Shortcut for 'status <id> pending'.");

        config.AddCommand<RemoveCommand>("rm")
            .WithDescription("Delete one or more tasks from the sheet.");

        config.AddCommand<SpacesCommand>("spaces")
            .WithDescription("List spaces (sheet tabs).");

        config.AddBranch("space", space =>
        {
            space.SetDescription("Manage spaces.");
            space.AddCommand<SpaceAddCommand>("add").WithDescription("Create a space.");
            space.AddCommand<SpaceRemoveCommand>("rm").WithDescription("Delete a space and its tasks.");
        });

        config.AddCommand<DoctorCommand>("doctor")
            .WithDescription("Check configuration, credentials and sheet schema.");

        config.AddCommand<ServerCommand>("server")
            .WithDescription("Serve the local dashboard.");

        config.AddCommand<ConfigCommand>("config")
            .WithDescription("Inspect or change configuration: config path|show|set <key> <value>.");
    }
}
