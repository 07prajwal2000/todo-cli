using Spectre.Console;
using Todo.Application.Common;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Cli.Infrastructure;

/// <summary>
/// All user-facing error rendering funnels through here so every command fails the same way: one
/// red line, field errors underneath, and the matching exit code.
/// </summary>
public sealed class ConsoleReporter(IAnsiConsole console)
{
    /// <summary>
    /// Legacy Windows code pages cannot render ✓/✗, and a broken glyph reads worse than plain ASCII,
    /// so the markers degrade with the terminal instead of assuming UTF-8.
    /// </summary>
    private string SuccessMarker => console.Profile.Capabilities.Unicode ? "✓" : "ok";

    private string FailureMarker => console.Profile.Capabilities.Unicode ? "✗" : "x";

    public void Success(string message) => console.MarkupLine($"[green]{SuccessMarker}[/] {Markup.Escape(message)}");

    public void Info(string message) => console.MarkupLine(Markup.Escape(message));

    public void Warn(string message) => console.MarkupLine($"[yellow]![/] {Markup.Escape(message)}");

    public int ReportError(TodoError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        console.MarkupLine($"[red]{FailureMarker}[/] {Markup.Escape(error.Message)}");

        if (error.Fields is { Count: > 0 } fields)
        {
            foreach (var (field, messages) in fields.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                foreach (var message in messages)
                {
                    // A field message that just repeats the headline is noise, not extra information.
                    if (string.Equals(message, error.Message, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    console.MarkupLine($"  [red]{Markup.Escape(field)}[/] {Markup.Escape(message)}");
                }
            }
        }

        return ExitCodeMapper.For(error.Code);
    }

    public void TaskTable(IReadOnlyList<TaskItem> tasks, SpaceName space, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        if (tasks.Count == 0)
        {
            console.MarkupLine($"[grey]No tasks in '{Markup.Escape(space.Value)}'.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn("[grey]#[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn("Status")
            .AddColumn("Due")
            .AddColumn("Created");

        foreach (var task in tasks)
        {
            table.AddRow(
                $"[grey]{task.Id.Value}[/]",
                FormatName(task),
                FormatStatus(task),
                FormatDue(task, today),
                $"[grey]{task.CreatedAt.UtcDateTime:yyyy-MM-dd}[/]");
        }

        console.Write(table);
    }

    public void TaskDetail(TaskItem task, SpaceName space, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(task);

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        grid.AddRow("[grey]id[/]", task.Id.Value.ToString());
        grid.AddRow("[grey]space[/]", Markup.Escape(space.Value));
        grid.AddRow("[grey]name[/]", Markup.Escape(task.Name));
        grid.AddRow("[grey]status[/]", FormatStatus(task));
        grid.AddRow("[grey]due[/]", FormatDue(task, today));
        grid.AddRow("[grey]created[/]", $"{task.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC");

        if (task.CompletedAt is { } completedAt)
        {
            grid.AddRow("[grey]completed[/]", $"{completedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC");
        }

        if (task.Description.Length > 0)
        {
            grid.AddRow("[grey]description[/]", Markup.Escape(task.Description));
        }

        if (task.Remarks.Length > 0)
        {
            grid.AddRow("[grey]remarks[/]", Markup.Escape(task.Remarks));
        }

        console.Write(grid);
    }

    private static string FormatName(TaskItem task)
    {
        var name = Markup.Escape(task.Name);

        return task.Status == TaskState.Completed ? $"[grey strikethrough]{name}[/]" : name;
    }

    private static string FormatStatus(TaskItem task)
    {
        var label = TaskStates.Canonical(task.Status);

        return task.Status switch
        {
            TaskState.Ongoing => $"[blue]{label}[/]",
            TaskState.Pending => $"[yellow]{label}[/]",
            TaskState.Backlog => $"[grey]{label}[/]",
            _ => $"[green]{label}[/]",
        };
    }

    private static string FormatDue(TaskItem task, DateOnly today)
    {
        if (task.DueOn is not { } due)
        {
            return "[grey]-[/]";
        }

        var days = due.Value.DayNumber - today.DayNumber;

        if (task.Status == TaskState.Completed)
        {
            return $"[grey]{due}[/]";
        }

        return days switch
        {
            < 0 => $"[red]{due} ({-days}d overdue)[/]",
            0 => $"[yellow]{due} (today)[/]",
            <= 7 => $"[yellow]{due} (in {days}d)[/]",
            _ => due.ToString(),
        };
    }
}
