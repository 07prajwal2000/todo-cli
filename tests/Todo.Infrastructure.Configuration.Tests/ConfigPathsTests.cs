namespace Todo.Infrastructure.Configuration.Tests;

public sealed class ConfigPathsTests : IDisposable
{
    private readonly string? _originalConfigDirectory =
        Environment.GetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable);

    private readonly string? _originalXdgConfigHome =
        Environment.GetEnvironmentVariable(ConfigPaths.XdgConfigHomeVariable);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, _originalConfigDirectory);
        Environment.SetEnvironmentVariable(ConfigPaths.XdgConfigHomeVariable, _originalXdgConfigHome);
    }

    [Fact]
    public void ConstructorOverrideWins()
    {
        using var temp = new TempDirectory();
        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, temp.File("from-env"));

        var paths = new ConfigPaths(temp.Path);

        Assert.Equal(temp.Path, paths.ConfigDirectory);
    }

    [Fact]
    public void FromEnvironmentReadsTheEnvironmentVariable()
    {
        using var temp = new TempDirectory();
        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, temp.File("from-env"));

        var paths = ConfigPaths.FromEnvironment();

        Assert.Equal(temp.File("from-env"), paths.ConfigDirectory);
    }

    [Fact]
    public void SettingsAndCredentialsFilesLiveInConfigDirectory()
    {
        using var temp = new TempDirectory();

        var paths = temp.ToConfigPaths();

        Assert.Equal(Path.Combine(temp.Path, "config.json"), paths.SettingsFilePath);
        Assert.Equal(Path.Combine(temp.Path, "credentials.json"), paths.CredentialsFilePath);
    }

    [Fact]
    public void TodoConfigDirectoryEnvironmentVariableIsHonored()
    {
        using var temp = new TempDirectory();
        var configured = temp.File("custom");
        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, configured);

        var paths = new ConfigPaths();

        Assert.Equal(configured, paths.ConfigDirectory);
    }

    [Fact]
    public void XdgConfigHomeIsHonoredOnPosix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempDirectory();
        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, null);
        Environment.SetEnvironmentVariable(ConfigPaths.XdgConfigHomeVariable, temp.Path);

        var paths = new ConfigPaths();

        Assert.Equal(Path.Combine(temp.Path, "todo"), paths.ConfigDirectory);
    }

    [Fact]
    public void FallsBackToHomeDotConfigOnPosix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, null);
        Environment.SetEnvironmentVariable(ConfigPaths.XdgConfigHomeVariable, null);

        var paths = new ConfigPaths();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, ".config", "todo"), paths.ConfigDirectory);
    }

    [Fact]
    public void EmptyXdgConfigHomeFallsBackToHomeDotConfigOnPosix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, null);
        Environment.SetEnvironmentVariable(ConfigPaths.XdgConfigHomeVariable, "   ");

        var paths = new ConfigPaths();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, ".config", "todo"), paths.ConfigDirectory);
    }

    [Fact]
    public void UsesApplicationDataOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Environment.SetEnvironmentVariable(ConfigPaths.ConfigDirectoryVariable, null);

        var paths = new ConfigPaths();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.Equal(Path.Combine(appData, "todo"), paths.ConfigDirectory);
    }
}
