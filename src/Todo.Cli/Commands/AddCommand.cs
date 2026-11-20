using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Common;
using Todo.Application.Spaces;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Domain.Tasks;

namespace Todo.Cli.Commands;

public sealed class AddCommand(
    TaskService tasks,
    SpaceService spaces,
    SpaceResolver resolver,
    ConsoleReporter reporter) : AsyncCommand<AddCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Task name (required).")]
        public required string Name { get; init; }

        [CommandOption("-d|--desc <TEXT>")]
        [Description("Longer description.")]
        public string? Description { get; init; }

        [CommandOption("--due <DATE>")]
        [Description("Due date as yyyy-MM-dd (optional).")]
        public string? Due { get; init; }

        [CommandOption("-t|--status <STATE>")]
        [Description("Initial state: pending (default), ongoing, backlog, completed.")]
        public string? Status { get; init; }

        [CommandOption("--remarks <TEXT>")]
        [Description("Free-form remarks.")]
        public string? Remarks { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!TryParseDue(settings.Due, out var dueOn, out var dueError))
        {
            return reporter.ReportError(TodoError.Validation(dueError!, Field("due", dueError!)));
        }

        if (!TryParseState(settings.Status, out var state, out var stateError))
        {
            return reporter.ReportError(TodoError.Validation(stateError!, Field("status", stateError!)));
        }

        var space = await resolver.ResolveAsync(settings.Space, cancellationToken).ConfigureAwait(false);
        if (!space.TryGetValue(out var target))
        {
            return reporter.ReportError(space.Error!);
        }

        var ensured = await spaces.EnsureExistsAsync(target, cancellationToken).ConfigureAwait(false);
        if (ensured.IsFailure)
        {
            return reporter.ReportError(ensured.Error!);
        }

        var created = await tasks.AddAsync(
            target,
            new TaskDraftInput
            {
                Name = settings.Name,
                Description = settings.Description,
                State = state,
                DueOn = dueOn,
                Remarks = settings.Remarks,
            },
            cancellationToken).ConfigureAwait(false);

        if (!created.TryGetValue(out var task))
        {
            return reporter.ReportError(created.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Task(task));
        }
        else
        {
            reporter.Success($"Added #{task.Id.Value} '{task.Name}' to '{target.Value}'.");
        }

        return ExitCodes.Success;
    }

    internal static bool TryParseDue(string? value, out DueDate? dueOn, out string? error)
    {
        dueOn = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DueDate.TryParse(value, out var parsed))
        {
            dueOn = parsed;
            return true;
        }

        error = $"'{value}' is not a valid date. Use yyyy-MM-dd, for example 2026-12-01.";
        return false;
    }

    internal static bool TryParseState(string? value, out TaskState state, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            state = TaskState.Pending;
            return true;
        }

        if (TaskStates.TryParse(value, out state))
        {
            return true;
        }

        error = $"'{value}' is not a valid state. Use one of: {TaskStates.DescribeAccepted()}.";
        return false;
    }

    internal static Dictionary<string, string[]> Field(string name, string message) => new() { [name] = [message] };
}
