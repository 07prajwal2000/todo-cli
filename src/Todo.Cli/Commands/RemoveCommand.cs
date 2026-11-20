using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Domain.Tasks;

namespace Todo.Cli.Commands;

public sealed class RemoveCommand(
    TaskService tasks,
    SpaceResolver resolver,
    ConsoleReporter reporter,
    IAnsiConsole console) : AsyncCommand<RemoveCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Task id, or a comma-separated list of ids.")]
        public required string Id { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip the confirmation prompt.")]
        public bool Yes { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var ids = ParseIds(settings.Id);
        if (ids.Count == 0)
        {
            return reporter.ReportError(Todo.Application.Common.TodoError.Validation(
                $"'{settings.Id}' does not contain any task ids."));
        }

        var space = await resolver.ResolveAsync(settings.Space, cancellationToken).ConfigureAwait(false);
        if (!space.TryGetValue(out var target))
        {
            return reporter.ReportError(space.Error!);
        }

        var found = new List<TaskItem>();
        foreach (var id in ids)
        {
            var task = await tasks.GetAsync(target, id, cancellationToken).ConfigureAwait(false);
            if (!task.TryGetValue(out var existing))
            {
                return reporter.ReportError(task.Error!);
            }

            found.Add(existing);
        }

        if (!settings.Yes && !Confirm(found, target))
        {
            reporter.Info("Cancelled.");
            return ExitCodes.Success;
        }

        foreach (var task in found)
        {
            var deleted = await tasks.DeleteAsync(target, task.Id, cancellationToken).ConfigureAwait(false);
            if (deleted.IsFailure)
            {
                return reporter.ReportError(deleted.Error!);
            }
        }

        reporter.Success(found.Count == 1
            ? $"Deleted #{found[0].Id.Value} '{found[0].Name}' from '{target.Value}'."
            : $"Deleted {found.Count} tasks from '{target.Value}'.");

        return ExitCodes.Success;
    }

    internal static IReadOnlyList<TaskId> ParseIds(string value)
    {
        var ids = new List<TaskId>();

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var number) && number > 0)
            {
                ids.Add(new TaskId(number));
            }
        }

        return ids;
    }

    private bool Confirm(List<TaskItem> tasks, Todo.Domain.Spaces.SpaceName space)
    {
        var table = string.Join(Environment.NewLine, tasks.Select(task => $"  #{task.Id.Value} {task.Name}"));

        var prompt = new ConfirmationPrompt(
            $"Delete {tasks.Count} task(s) from '{space.Value}'?{Environment.NewLine}{table}")
        {
            DefaultValue = false,
        };

        return console.Prompt(prompt);
    }
}
