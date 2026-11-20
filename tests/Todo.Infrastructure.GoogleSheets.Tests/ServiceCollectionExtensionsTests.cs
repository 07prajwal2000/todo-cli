using Microsoft.Extensions.DependencyInjection;
using Todo.Application.Spaces;
using Todo.Application.Tasks;

namespace Todo.Infrastructure.GoogleSheets.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGoogleSheetsInfrastructure_registers_every_port_as_a_singleton()
    {
        var services = new ServiceCollection();

        services.AddGoogleSheetsInfrastructure();

        AssertRegistered<SheetsConnectionFactory>(services);
        AssertRegistered<IRequestExecutor>(services);
        AssertRegistered<SheetsGateway>(services);
        AssertRegistered<ITaskRepository>(services);
        AssertRegistered<ISpaceRepository>(services);
        AssertRegistered<IBackendProbe>(services);
    }

    [Fact]
    public void AddGoogleSheetsInfrastructure_binds_the_ports_to_the_sheets_implementations()
    {
        var services = new ServiceCollection();

        services.AddGoogleSheetsInfrastructure();

        Assert.Contains(services, d => d.ServiceType == typeof(ITaskRepository) && d.ImplementationType == typeof(SheetsTaskRepository));
        Assert.Contains(services, d => d.ServiceType == typeof(ISpaceRepository) && d.ImplementationType == typeof(SheetsSpaceRepository));
        Assert.Contains(services, d => d.ServiceType == typeof(IBackendProbe) && d.ImplementationType == typeof(SheetsBackendProbe));
    }

    [Fact]
    public void AddGoogleSheetsInfrastructure_is_idempotent()
    {
        var services = new ServiceCollection();

        services.AddGoogleSheetsInfrastructure();
        services.AddGoogleSheetsInfrastructure();

        Assert.Single(services, d => d.ServiceType == typeof(ITaskRepository));
        Assert.Single(services, d => d.ServiceType == typeof(SheetsGateway));
    }

    [Fact]
    public void AddGoogleSheetsInfrastructure_rejects_a_null_collection()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddGoogleSheetsInfrastructure(null!));
    }

    private static void AssertRegistered<TService>(IServiceCollection services) =>
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(TService) && d.Lifetime == ServiceLifetime.Singleton);
}
