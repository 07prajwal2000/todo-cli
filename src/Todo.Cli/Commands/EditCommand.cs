using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Common;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Domain.Tasks;

namespace Todo.Cli.Commands;

public sealed class EditCommand(
    TaskService tasks,
    SpaceResolver resolver,
    ConsoleReporter reporter) : AsyncCommand<EditCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Task id.")]
        public required int Id { get; init; }

        [CommandOption("--name <TEXT>")]
        [Description("Replace the name.")]
        public string? Name { get; init; }

        [CommandOption("-d|--desc <TEXT>")]
        [Description("Replace the description.")]
        public string? Description { get; init; }

        [CommandOption("--due <DATE>")]
        [Description("Set the due date (yyyy-MM-dd).")]
        public string? Due { get; init; }

        [CommandOption("--clear-due")]
        [Description("Remove the due date.")]
        public bool ClearDue { get; init; }

        [CommandOption("--remarks <TEXT>")]
        [Description("Replace the remarks.")]
        public string? Remarks { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.ClearDue && !string.IsNullOrWhiteSpace(settings.Due))
        {
            const string message = "Use either --due or --clear-due, not both.";
            return reporter.ReportError(TodoError.Validation(message));
        }

        if (!AddCommand.TryParseDue(settings.Due, out var dueOn, out var dueError))
        {
            return reporter.ReportError(TodoError.Validation(dueError!, AddCommand.Field("due", dueError!)));
        }

        if (settings.Name is null && settings.Description is null && dueOn is null && !settings.ClearDue && settings.Remarks is null)
        {
            return reporter.ReportError(TodoError.Validation(
                "Nothing to change. Pass at least one of --name, --desc, --due, --clear-due or --remarks."));
        }

        var space = await resolver.ResolveAsync(settings.Space, cancellationToken).ConfigureAwait(false);
        if (!space.TryGetValue(out var target))
        {
            return reporter.ReportError(space.Error!);
        }

        var edited = await tasks.EditAsync(
            target,
            new TaskId(settings.Id),
            new TaskEditInput
            {
                Name = settings.Name,
                Description = settings.Description,
                DueOn = dueOn,
                ClearDue = settings.ClearDue,
                Remarks = settings.Remarks,
            },
            cancellationToken).ConfigureAwait(false);

        if (!edited.TryGetValue(out var task))
        {
            return reporter.ReportError(edited.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Task(task));
        }
        else
        {
            reporter.Success($"Updated #{task.Id.Value} '{task.Name}'.");
        }

        return ExitCodes.Success;
    }
}
