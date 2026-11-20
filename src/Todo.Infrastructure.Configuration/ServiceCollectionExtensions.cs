using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfigurationInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddConfigurationInfrastructure(ConfigPaths.FromEnvironment());
    }

    public static IServiceCollection AddConfigurationInfrastructure(this IServiceCollection services, ConfigPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.TryAddSingleton(paths);
        services.TryAddSingleton<ISettingsStore, JsonSettingsStore>();
        services.TryAddSingleton<ICredentialStore, FileCredentialStore>();
        services.TryAddSingleton<IClock, SystemClock>();

        return services;
    }
}
