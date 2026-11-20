using Spectre.Console;
using Spectre.Console.Cli;
using Todo.Cli.Composition;
using Todo.Cli.Infrastructure;

namespace Todo.Cli;

public static class Program
{
    private static string FailureMarker => AnsiConsole.Profile.Capabilities.Unicode ? "✗" : "x";

    public static async Task<int> Main(string[] args)
    {
        var services = ServiceRegistration.Build();

        var app = new CommandApp(new TypeRegistrar(services));

        app.Configure(configuration =>
        {
            configuration.SetExceptionHandler((exception, _) => Handle(exception));
            CommandRegistrar.Register(configuration);
        });

        return await app.RunAsync(args).ConfigureAwait(false);
    }

    /// <summary>
    /// Users get one red line instead of a stack trace, while a genuine bug still exits non-zero so
    /// scripts notice. Parse errors are usage errors, which is a distinct exit code.
    /// </summary>
    private static int Handle(Exception exception) => exception switch
    {
        CommandParseException parse => Render($"[red]{FailureMarker}[/] {Markup.Escape(parse.Message)}", ExitCodes.Usage),
        OperationCanceledException => Render("[grey]Cancelled.[/]", ExitCodes.Success),
        _ => Render($"[red]{FailureMarker}[/] Unexpected error: {Markup.Escape(exception.Message)}", ExitCodes.Failure),
    };

    private static int Render(string markup, int exitCode)
    {
        AnsiConsole.MarkupLine(markup);
        return exitCode;
    }
}
