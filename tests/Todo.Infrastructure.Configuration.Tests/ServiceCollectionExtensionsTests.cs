using Microsoft.Extensions.DependencyInjection;
using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Infrastructure.Configuration.Tests;

public sealed class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AddConfigurationInfrastructureRegistersAllServices()
    {
        var services = new ServiceCollection();

        services.AddConfigurationInfrastructure(_temp.ToConfigPaths());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider;

        Assert.NotNull(resolved.GetRequiredService<ConfigPaths>());
        Assert.IsType<JsonSettingsStore>(resolved.GetRequiredService<ISettingsStore>());
        Assert.IsType<FileCredentialStore>(resolved.GetRequiredService<ICredentialStore>());
        Assert.IsType<SystemClock>(resolved.GetRequiredService<IClock>());
    }

    [Fact]
    public void RegistersTheSuppliedConfigPathsInstance()
    {
        var paths = _temp.ToConfigPaths();
        var services = new ServiceCollection();

        services.AddConfigurationInfrastructure(paths);

        using var provider = services.BuildServiceProvider();

        Assert.Same(paths, provider.GetRequiredService<ConfigPaths>());
    }

    [Fact]
    public void ResolvedStoresUseTheSuppliedPaths()
    {
        var paths = _temp.ToConfigPaths();
        var services = new ServiceCollection();
        services.AddConfigurationInfrastructure(paths);

        using var provider = services.BuildServiceProvider();

        Assert.Equal(paths.SettingsFilePath, provider.GetRequiredService<ISettingsStore>().DescribeLocation());
        Assert.Equal(paths.CredentialsFilePath, provider.GetRequiredService<ICredentialStore>().DescribeLocation());
    }

    [Fact]
    public void EnvironmentOverloadRegistersConfigPaths()
    {
        var services = new ServiceCollection();

        services.AddConfigurationInfrastructure();

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<ConfigPaths>();

        Assert.Equal(ConfigPaths.FromEnvironment().ConfigDirectory, resolved.ConfigDirectory);
    }

    [Fact]
    public void ReturnsTheSameCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddConfigurationInfrastructure(_temp.ToConfigPaths()));
    }
}
