using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Application.Common;
using Todo.Application.Notes;
using Todo.Cli.Infrastructure;

namespace Todo.Cli.Commands;

public class NoteSettings : CommandSettings
{
    [CommandOption("--json")]
    [Description("Emit machine-readable JSON.")]
    public bool Json { get; init; }
}

/// <summary>Settings for commands that take markdown, either inline or from a file.</summary>
public class NoteContentSettings : NoteSettings
{
    [CommandOption("-c|--content <MARKDOWN>")]
    [Description("Markdown content.")]
    public string? Content { get; init; }

    [CommandOption("-f|--file <PATH>")]
    [Description("Read the content from a file.")]
    public string? File { get; init; }

    [CommandOption("--stdin")]
    [Description("Read the content from standard input.")]
    public bool Stdin { get; init; }

    internal async Task<Result<string?>> ResolveAsync(CancellationToken cancellationToken)
    {
        if ((Content is not null ? 1 : 0) + (File is not null ? 1 : 0) + (Stdin ? 1 : 0) > 1)
        {
            return TodoError.Validation("Use only one of --content, --file or --stdin.");
        }

        try
        {
            return Stdin ? await Console.In.ReadToEndAsync(cancellationToken).ConfigureAwait(false)
                : File is null ? Content
                : await System.IO.File.ReadAllTextAsync(File, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TodoError.Validation($"Cannot read '{File}': {exception.Message}");
        }
    }
}

public sealed class NoteAddCommand(NoteService notes, ConsoleReporter reporter)
    : AsyncCommand<NoteAddCommand.Settings>
{
    public sealed class Settings : NoteContentSettings
    {
        [CommandArgument(0, "<TITLE>")]
        [Description("Note title (required).")]
        public required string Title { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var content = await settings.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (!content.TryGetValue(out var markdown))
        {
            return reporter.ReportError(content.Error!);
        }

        var added = await notes.AddAsync(settings.Title, markdown, cancellationToken).ConfigureAwait(false);
        if (!added.TryGetValue(out var note))
        {
            return reporter.ReportError(added.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Note(note));
        }
        else
        {
            reporter.Success($"Added note #{note.Id} '{note.Title}'.");
        }

        return ExitCodes.Success;
    }
}

public sealed class NoteListCommand(NoteService notes, ConsoleReporter reporter, IAnsiConsole console)
    : AsyncCommand<NoteListCommand.Settings>
{
    public sealed class Settings : NoteSettings
    {
        [CommandOption("-q|--search <TEXT>")]
        [Description("Only notes whose title or content contains this text.")]
        public string? Search { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var listed = await notes.ListAsync(settings.Search, cancellationToken).ConfigureAwait(false);
        if (!listed.TryGetValue(out var items))
        {
            return reporter.ReportError(listed.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.NoteList(items));
        }
        else if (items.Count == 0)
        {
            console.MarkupLine("[grey]No notes.[/]");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.None)
                .AddColumn(new TableColumn("[grey]#[/]").RightAligned())
                .AddColumn("Title")
                .AddColumn("Created");

            foreach (var note in items)
            {
                table.AddRow($"[grey]{note.Id}[/]", Markup.Escape(note.Title), $"[grey]{note.CreatedAt.UtcDateTime:yyyy-MM-dd}[/]");
            }

            console.Write(table);
        }

        return ExitCodes.Success;
    }
}

public sealed class NoteShowCommand(NoteService notes, ConsoleReporter reporter, IAnsiConsole console)
    : AsyncCommand<NoteShowCommand.Settings>
{
    public sealed class Settings : NoteSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Note id.")]
        public required int Id { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var found = await notes.GetAsync(settings.Id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var note))
        {
            return reporter.ReportError(found.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Note(note));
        }
        else
        {
            console.MarkupLine($"[bold]#{note.Id} {Markup.Escape(note.Title)}[/] [grey]{note.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC[/]");
            console.WriteLine();
            console.WriteLine(note.Content);
        }

        return ExitCodes.Success;
    }
}

public sealed class NoteEditCommand(NoteService notes, ConsoleReporter reporter)
    : AsyncCommand<NoteEditCommand.Settings>
{
    public sealed class Settings : NoteContentSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Note id.")]
        public required int Id { get; init; }

        [CommandOption("--title <TEXT>")]
        [Description("Replace the title.")]
        public string? Title { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var content = await settings.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (content.IsFailure)
        {
            return reporter.ReportError(content.Error!);
        }

        var markdown = content.ValueOrDefault;

        if (settings.Title is null && markdown is null)
        {
            return reporter.ReportError(TodoError.Validation("Nothing to change. Pass at least one of --title, --content, --file or --stdin."));
        }

        var edited = await notes.EditAsync(settings.Id, settings.Title, markdown, cancellationToken).ConfigureAwait(false);
        if (!edited.TryGetValue(out var note))
        {
            return reporter.ReportError(edited.Error!);
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonOutput.Note(note));
        }
        else
        {
            reporter.Success($"Updated note #{note.Id} '{note.Title}'.");
        }

        return ExitCodes.Success;
    }
}

public sealed class NoteRemoveCommand(NoteService notes, ConsoleReporter reporter, IAnsiConsole console)
    : AsyncCommand<NoteRemoveCommand.Settings>
{
    public sealed class Settings : NoteSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Note id.")]
        public required int Id { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip the confirmation prompt.")]
        public bool Yes { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var found = await notes.GetAsync(settings.Id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var note))
        {
            return reporter.ReportError(found.Error!);
        }

        if (!settings.Yes && !console.Prompt(new ConfirmationPrompt($"Delete note #{note.Id} '{Markup.Escape(note.Title)}'?") { DefaultValue = false }))
        {
            reporter.Info("Cancelled.");
            return ExitCodes.Success;
        }

        var deleted = await notes.DeleteAsync(note.Id, cancellationToken).ConfigureAwait(false);
        if (deleted.IsFailure)
        {
            return reporter.ReportError(deleted.Error!);
        }

        reporter.Success($"Deleted note #{note.Id} '{note.Title}'.");
        return ExitCodes.Success;
    }
}
