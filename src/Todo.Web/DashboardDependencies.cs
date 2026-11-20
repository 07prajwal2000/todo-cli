using Todo.Application.Common;
using Todo.Application.Notes;
using Todo.Application.Spaces;
using Todo.Application.Tasks;

namespace Todo.Web;

/// <summary>
/// Everything the dashboard binds itself to. Assembled by the CLI so the web surface never
/// reaches into configuration or the composition root on its own.
/// </summary>
public sealed record DashboardDependencies
{
    public required TaskService TaskService { get; init; }

    public required NoteService NoteService { get; init; }

    public required SpaceService SpaceService { get; init; }

    public required IClock Clock { get; init; }

    public string? DefaultSpace { get; init; }

    public string Provider { get; init; } = "google-sheets";

    public string? SpreadsheetId { get; init; }
}
