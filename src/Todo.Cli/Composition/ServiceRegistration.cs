using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Todo.Application.Notes;
using Todo.Application.Setup;
using Todo.Application.Spaces;
using Todo.Application.Tasks;
using Todo.Cli.Infrastructure;
using Todo.Infrastructure.Configuration;
using Todo.Infrastructure.GoogleSheets;

namespace Todo.Cli.Composition;

/// <summary>
/// The composition root. This is the only place that knows every layer exists; command code resolves
/// ports from the container and never names an adapter.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection Build(IServiceProvider? existing = null)
    {
        var services = new ServiceCollection();

        if (existing is not null)
        {
            services.AddSingleton(existing);
        }

        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
        services.AddSingleton<ConsoleReporter>();
        services.AddSingleton<SpaceResolver>();

        services.AddConfigurationInfrastructure();
        services.AddGoogleSheetsInfrastructure();

        services.AddSingleton<TaskService>();
        services.AddSingleton<NoteService>();
        services.AddSingleton<SpaceService>();
        services.AddSingleton<SetupService>();
        services.AddSingleton<DiagnosticsService>();

        return services;
    }
}
