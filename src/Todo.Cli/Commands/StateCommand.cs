using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Domain.Tasks;

namespace Todo.Cli.Commands;

public sealed class StateCommand(
    TaskService tasks,
    SpaceResolver resolver,
    ConsoleReporter reporter) : AsyncCommand<StateCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Task id.")]
        public required int Id { get; init; }

        [CommandArgument(1, "<STATE>")]
        [Description("Target state: backlog, pending, ongoing, completed (aliases like done/wip accepted).")]
        public required string State { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!AddCommand.TryParseState(settings.State, out var target, out var error))
        {
            return reporter.ReportError(Todo.Application.Common.TodoError.Validation(
                error!,
                AddCommand.Field("state", error!)));
        }

        return await StateCommands.ApplyAsync(tasks, resolver, reporter, settings, target, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// The three shortcut verbs (<c>done</c>, <c>start</c>, <c>reopen</c>) are the same operation with a
/// fixed target, so they share this implementation instead of repeating the plumbing.
/// </summary>
internal static class StateCommands
{
    public static async Task<int> ApplyAsync(
        TaskService tasks,
        SpaceResolver resolver,
        ConsoleReporter reporter,
        GlobalSettings settings,
        TaskState target,
        CancellationToken cancellationToken)
    {
        var space = await resolver.ResolveAsync(settings.Space, cancellationToken).ConfigureAwait(false);
        if (!space.TryGetValue(out var resolved))
        {
            return reporter.ReportError(space.Error!);
        }

        var id = settings switch
        {
            StateCommand.Settings state => state.Id,
            ShortcutSettings shortcut => shortcut.Id,
            _ => throw new InvalidOperationException($"Unsupported settings type {settings.GetType().Name}."),
        };

        var changed = await tasks.ChangeStateAsync(resolved, new TaskId(id), target, cancellationToken)
            .ConfigureAwait(false);

        if (!changed.TryGetValue(out var task))
        {
            return reporter.ReportError(changed.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Task(task));
        }
        else
        {
            reporter.Success($"#{task.Id.Value} '{task.Name}' is now {TaskStates.Canonical(task.Status)}.");
        }

        return ExitCodes.Success;
    }
}

public abstract class ShortcutSettings : GlobalSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("Task id.")]
    public required int Id { get; init; }
}

public sealed class DoneCommand(TaskService tasks, SpaceResolver resolver, ConsoleReporter reporter)
    : AsyncCommand<DoneCommand.Settings>
{
    public sealed class Settings : ShortcutSettings;

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        StateCommands.ApplyAsync(tasks, resolver, reporter, settings, TaskState.Completed, cancellationToken);
}

public sealed class StartCommand(TaskService tasks, SpaceResolver resolver, ConsoleReporter reporter)
    : AsyncCommand<StartCommand.Settings>
{
    public sealed class Settings : ShortcutSettings;

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        StateCommands.ApplyAsync(tasks, resolver, reporter, settings, TaskState.Ongoing, cancellationToken);
}

public sealed class ReopenCommand(TaskService tasks, SpaceResolver resolver, ConsoleReporter reporter)
    : AsyncCommand<ReopenCommand.Settings>
{
    public sealed class Settings : ShortcutSettings;

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        StateCommands.ApplyAsync(tasks, resolver, reporter, settings, TaskState.Pending, cancellationToken);
}
