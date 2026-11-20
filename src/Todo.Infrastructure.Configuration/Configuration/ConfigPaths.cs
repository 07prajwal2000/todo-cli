namespace Todo.Infrastructure.Configuration;

public sealed class ConfigPaths
{
    public const string ConfigDirectoryVariable = "TODO_CONFIG_DIR";

    public const string XdgConfigHomeVariable = "XDG_CONFIG_HOME";

    public const string ConfigFileName = "config.json";

    public const string CredentialsFileName = "credentials.json";

    private const string ApplicationFolderName = "todo";

    public ConfigPaths(string? configDirectoryOverride = null) =>
        ConfigDirectory = ResolveDirectory(configDirectoryOverride);

    public string ConfigDirectory { get; }

    public string SettingsFilePath => Path.Combine(ConfigDirectory, ConfigFileName);

    public string CredentialsFilePath => Path.Combine(ConfigDirectory, CredentialsFileName);

    public static ConfigPaths FromEnvironment() => new();

    private static string ResolveDirectory(string? configDirectoryOverride)
    {
        if (!string.IsNullOrWhiteSpace(configDirectoryOverride))
        {
            return Path.TrimEndingDirectorySeparator(configDirectoryOverride);
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(ConfigDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.TrimEndingDirectorySeparator(fromEnvironment);
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, ApplicationFolderName);
        }

        // macOS follows the XDG layout on purpose: one documented location for every POSIX host
        // beats a per-platform special case.
        var xdgConfigHome = Environment.GetEnvironmentVariable(XdgConfigHomeVariable);
        var configHome = string.IsNullOrWhiteSpace(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        return Path.Combine(configHome, ApplicationFolderName);
    }
}
