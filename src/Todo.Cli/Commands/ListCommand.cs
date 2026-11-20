using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Common;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Domain.Tasks;

namespace Todo.Cli.Commands;

public sealed class ListCommand(
    TaskService tasks,
    SpaceResolver resolver,
    ConsoleReporter reporter,
    IClock clock) : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--status <STATE>")]
        [Description("Filter by state; comma-separate for several (pending,ongoing,backlog,completed).")]
        public string? Status { get; init; }

        [CommandOption("--all")]
        [Description("Include backlog and completed tasks.")]
        public bool All { get; init; }

        [CommandOption("--due-before <DATE>")]
        [Description("Only tasks due on or before this yyyy-MM-dd date.")]
        public string? DueBefore { get; init; }

        [CommandOption("--sort <FIELD>")]
        [Description("Sort by: default, due, id, status, created.")]
        public string? Sort { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var states = new List<TaskState>();

        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            foreach (var part in settings.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!TaskStates.TryParse(part, out var state))
                {
                    return reporter.ReportError(TodoError.Validation(
                        $"'{part}' is not a valid state. Use one of: {TaskStates.DescribeAccepted()}.",
                        AddCommand.Field("status", $"Unknown state '{part}'.")));
                }

                states.Add(state);
            }
        }

        DueDate? dueBefore = null;
        if (!AddCommand.TryParseDue(settings.DueBefore, out dueBefore, out var dueError))
        {
            return reporter.ReportError(TodoError.Validation(dueError!, AddCommand.Field("dueBefore", dueError!)));
        }

        if (!TryParseSort(settings.Sort, out var sortBy, out var sortError))
        {
            return reporter.ReportError(TodoError.Validation(sortError!, AddCommand.Field("sort", sortError!)));
        }

        var space = await resolver.ResolveAsync(settings.Space, cancellationToken).ConfigureAwait(false);
        if (!space.TryGetValue(out var target))
        {
            return reporter.ReportError(space.Error!);
        }

        var listed = await tasks.ListAsync(
            target,
            new TaskQuery
            {
                States = states,
                DueBefore = dueBefore,
                SortBy = sortBy,
                IncludeAllStates = settings.All,
            },
            cancellationToken).ConfigureAwait(false);

        if (!listed.TryGetValue(out var items))
        {
            return reporter.ReportError(listed.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.TaskList(items, target));
        }
        else
        {
            reporter.TaskTable(items, target, clock.Today);
        }

        return ExitCodes.Success;
    }

    private static bool TryParseSort(string? value, out TaskSortField sortBy, out string? error)
    {
        error = null;
        sortBy = TaskSortField.Default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parsed = value.Trim().ToLowerInvariant() switch
        {
            "default" => TaskSortField.Default,
            "due" => TaskSortField.Due,
            "id" => TaskSortField.Id,
            "status" or "state" => TaskSortField.Status,
            "created" => TaskSortField.Created,
            _ => (TaskSortField?)null,
        };

        if (parsed is null)
        {
            error = $"'{value}' is not a valid sort field. Use default, due, id, status or created.";
            return false;
        }

        sortBy = parsed.Value;
        return true;
    }
}
