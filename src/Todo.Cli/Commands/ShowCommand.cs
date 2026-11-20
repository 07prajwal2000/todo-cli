using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Common;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Domain.Tasks;

namespace Todo.Cli.Commands;

public sealed class ShowCommand(
    TaskService tasks,
    SpaceResolver resolver,
    ConsoleReporter reporter,
    IClock clock) : AsyncCommand<ShowCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Task id.")]
        public required int Id { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var space = await resolver.ResolveAsync(settings.Space, cancellationToken).ConfigureAwait(false);
        if (!space.TryGetValue(out var target))
        {
            return reporter.ReportError(space.Error!);
        }

        var found = await tasks.GetAsync(target, new TaskId(settings.Id), cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var task))
        {
            return reporter.ReportError(found.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Task(task));
        }
        else
        {
            reporter.TaskDetail(task, target, clock.Today);
        }

        return ExitCodes.Success;
    }
}
