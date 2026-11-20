using System.Reflection;

namespace Todo.Architecture.Tests;

/// <summary>
/// Shared handles for the six production assemblies.
/// </summary>
public static class Assemblies
{
    /// <summary>
    /// The CLI assembly is called <c>todo</c> (the shipped binary name). Module-reference checks must
    /// use that name, while namespace-based rules must use <see cref="CliNamespace"/>: NetArchTest
    /// compares dependency names as case-insensitive prefixes, so the assembly name <c>todo</c>
    /// would otherwise match every <c>Todo.*</c> namespace.
    /// </summary>
    public const string CliAssemblyName = "todo";

    public const string DomainNamespace = "Todo.Domain";
    public const string ApplicationNamespace = "Todo.Application";
    public const string GoogleSheetsNamespace = "Todo.Infrastructure.GoogleSheets";
    public const string ConfigurationNamespace = "Todo.Infrastructure.Configuration";
    public const string WebNamespace = "Todo.Web";
    public const string CliNamespace = "Todo.Cli";

    public const string GoogleApis = "Google.Apis";
    public const string MicrosoftExtensions = "Microsoft.Extensions";

    public static Assembly Domain { get; } = typeof(Todo.Domain.Tasks.TaskItem).Assembly;

    public static Assembly Application { get; } = typeof(Todo.Application.Common.Result<>).Assembly;

    public static Assembly GoogleSheets { get; } =
        typeof(Todo.Infrastructure.GoogleSheets.SheetsConnectionFactory).Assembly;

    public static Assembly Configuration { get; } =
        typeof(Todo.Infrastructure.Configuration.ServiceCollectionExtensions).Assembly;

    public static Assembly Web { get; } = typeof(Todo.Web.DashboardDependencies).Assembly;

    /// <summary>
    /// The CLI declares no public type of its own, so it is resolved by assembly name. The project
    /// reference in this test project still guarantees that it sits next to the test binaries.
    /// </summary>
    public static Assembly Cli { get; } = Assembly.Load(CliAssemblyName);

    public static IReadOnlyList<Assembly> All { get; } = [Domain, Application, GoogleSheets, Configuration, Web, Cli];
}
