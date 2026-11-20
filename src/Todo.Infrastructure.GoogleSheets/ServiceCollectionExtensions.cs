using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Todo.Application.Notes;
using Todo.Application.Spaces;
using Todo.Application.Tasks;

namespace Todo.Infrastructure.GoogleSheets;

/// <summary>
/// Composition root for the Google Sheets backend. Every registration is a singleton because the
/// services hold only per-process caches; the credential and spreadsheet lookups are serialized
/// inside <see cref="SheetsConnectionFactory"/> rather than by lifetime scoping.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleSheetsInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddLogger();
        services.TryAddSingleton<RetryPolicy>(_ => new RetryPolicy());
        services.TryAddSingleton<IRequestExecutor>(provider => new GoogleRequestExecutor(
            provider.GetService<ILogger<GoogleRequestExecutor>>(),
            provider.GetRequiredService<RetryPolicy>()));
        services.TryAddSingleton<SheetsConnectionFactory>();
        services.TryAddSingleton<SheetsGateway>();
        services.TryAddSingleton<ITaskRepository, SheetsTaskRepository>();
        services.TryAddSingleton<INoteRepository, SheetsNoteRepository>();
        services.TryAddSingleton<ISpaceRepository, SheetsSpaceRepository>();
        services.TryAddSingleton<IBackendProbe, SheetsBackendProbe>();

        return services;
    }

    /// <summary>
    /// Logging is optional for consumers of this adapter, so a null logger is registered rather than
    /// forcing every host (CLI, web, tests) to call AddLogging before this method.
    /// </summary>
    private static void TryAddLogger(this IServiceCollection services) =>
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
}
