using System.Reflection;
using NetArchTest.Rules;

namespace Todo.Architecture.Tests;

/// <summary>
/// The dependency rules that keep the hexagonal layering intact. Each edge gets its own fact so a
/// violation names exactly one broken dependency.
/// </summary>
/// <remarks>
/// Two mechanisms are used on purpose:
/// <list type="bullet">
/// <item>NetArchTest type rules catch a single type reaching across a layer.</item>
/// <item>Module-reference assertions catch a project/package reference even when no type uses it yet,
/// which matters for the hosts because they declare almost no types.</item>
/// </list>
/// </remarks>
public sealed class LayerDependencyTests
{
    [Fact]
    public void DomainDoesNotDependOnApplication()
    {
        AssertNoTypeDependency(Assemblies.Domain, Assemblies.ApplicationNamespace);
        References.AssertNoModuleReference(Assemblies.Domain, Assemblies.ApplicationNamespace);
    }

    [Fact]
    public void DomainDoesNotDependOnGoogleSheetsInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.Domain, Assemblies.GoogleSheetsNamespace);
        References.AssertNoModuleReference(Assemblies.Domain, Assemblies.GoogleSheetsNamespace);
    }

    [Fact]
    public void DomainDoesNotDependOnConfigurationInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.Domain, Assemblies.ConfigurationNamespace);
        References.AssertNoModuleReference(Assemblies.Domain, Assemblies.ConfigurationNamespace);
    }

    [Fact]
    public void DomainDoesNotDependOnWeb()
    {
        AssertNoTypeDependency(Assemblies.Domain, Assemblies.WebNamespace);
        References.AssertNoModuleReference(Assemblies.Domain, Assemblies.WebNamespace);
    }

    [Fact]
    public void DomainDoesNotDependOnCli()
    {
        AssertNoTypeDependency(Assemblies.Domain, Assemblies.CliNamespace);
        References.AssertNoModuleReference(Assemblies.Domain, Assemblies.CliAssemblyName);
    }

    [Fact]
    public void DomainDoesNotDependOnGoogleApis()
    {
        AssertNoTypeDependency(Assemblies.Domain, Assemblies.GoogleApis);
        References.AssertNoModuleReference(Assemblies.Domain, Assemblies.GoogleApis);
    }

    [Fact]
    public void DomainDoesNotDependOnMicrosoftExtensions()
    {
        AssertNoTypeDependency(Assemblies.Domain, Assemblies.MicrosoftExtensions);
        References.AssertNoModuleReference(Assemblies.Domain, Assemblies.MicrosoftExtensions);
    }

    [Fact]
    public void DomainReferencesNothingButTheBaseClassLibrary()
    {
        var external = References.ReferencedNames(Assemblies.Domain)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal))
            .Where(name => !name.StartsWith("netstandard", StringComparison.Ordinal))
            .Where(name => !name.StartsWith("Microsoft.NETCore", StringComparison.Ordinal))
            .Where(name => !name.Equals("mscorlib", StringComparison.Ordinal))
            .Where(name => !name.Equals(Assemblies.DomainNamespace, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            external.Length == 0,
            $"Todo.Domain must reference nothing but the BCL so it stays reusable and package-free, " +
            $"but it references [{string.Join(", ", external)}].");
    }

    [Fact]
    public void ApplicationDoesNotDependOnGoogleSheetsInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.Application, Assemblies.GoogleSheetsNamespace);
        References.AssertNoModuleReference(Assemblies.Application, Assemblies.GoogleSheetsNamespace);
    }

    [Fact]
    public void ApplicationDoesNotDependOnConfigurationInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.Application, Assemblies.ConfigurationNamespace);
        References.AssertNoModuleReference(Assemblies.Application, Assemblies.ConfigurationNamespace);
    }

    [Fact]
    public void ApplicationDoesNotDependOnWeb()
    {
        AssertNoTypeDependency(Assemblies.Application, Assemblies.WebNamespace);
        References.AssertNoModuleReference(Assemblies.Application, Assemblies.WebNamespace);
    }

    [Fact]
    public void ApplicationDoesNotDependOnCli()
    {
        AssertNoTypeDependency(Assemblies.Application, Assemblies.CliNamespace);
        References.AssertNoModuleReference(Assemblies.Application, Assemblies.CliAssemblyName);
    }

    [Fact]
    public void ApplicationDoesNotDependOnGoogleApis()
    {
        AssertNoTypeDependency(Assemblies.Application, Assemblies.GoogleApis);
        References.AssertNoModuleReference(Assemblies.Application, Assemblies.GoogleApis);
    }

    [Fact]
    public void GoogleSheetsInfrastructureDoesNotDependOnWeb()
    {
        AssertNoTypeDependency(Assemblies.GoogleSheets, Assemblies.WebNamespace);
        References.AssertNoModuleReference(Assemblies.GoogleSheets, Assemblies.WebNamespace);
    }

    [Fact]
    public void GoogleSheetsInfrastructureDoesNotDependOnCli()
    {
        AssertNoTypeDependency(Assemblies.GoogleSheets, Assemblies.CliNamespace);
        References.AssertNoModuleReference(Assemblies.GoogleSheets, Assemblies.CliAssemblyName);
    }

    [Fact]
    public void GoogleSheetsInfrastructureDoesNotDependOnConfigurationInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.GoogleSheets, Assemblies.ConfigurationNamespace);
        References.AssertNoModuleReference(Assemblies.GoogleSheets, Assemblies.ConfigurationNamespace);
    }

    [Fact]
    public void ConfigurationInfrastructureDoesNotDependOnWeb()
    {
        AssertNoTypeDependency(Assemblies.Configuration, Assemblies.WebNamespace);
        References.AssertNoModuleReference(Assemblies.Configuration, Assemblies.WebNamespace);
    }

    [Fact]
    public void ConfigurationInfrastructureDoesNotDependOnCli()
    {
        AssertNoTypeDependency(Assemblies.Configuration, Assemblies.CliNamespace);
        References.AssertNoModuleReference(Assemblies.Configuration, Assemblies.CliAssemblyName);
    }

    [Fact]
    public void ConfigurationInfrastructureDoesNotDependOnGoogleSheetsInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.Configuration, Assemblies.GoogleSheetsNamespace);
        References.AssertNoModuleReference(Assemblies.Configuration, Assemblies.GoogleSheetsNamespace);
    }

    [Fact]
    public void WebDoesNotDependOnGoogleSheetsInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.Web, Assemblies.GoogleSheetsNamespace);
        References.AssertNoModuleReference(Assemblies.Web, Assemblies.GoogleSheetsNamespace);
    }

    [Fact]
    public void WebDoesNotDependOnConfigurationInfrastructure()
    {
        AssertNoTypeDependency(Assemblies.Web, Assemblies.ConfigurationNamespace);
        References.AssertNoModuleReference(Assemblies.Web, Assemblies.ConfigurationNamespace);
    }

    [Fact]
    public void OnlyGoogleSheetsInfrastructureReferencesGoogleApisTypes()
    {
        foreach (var assembly in Assemblies.All.Where(candidate => candidate != Assemblies.GoogleSheets))
        {
            AssertNoTypeDependency(assembly, Assemblies.GoogleApis);
        }
    }

    [Fact]
    public void OnlyGoogleSheetsInfrastructureReferencesGoogleApisAssemblies()
    {
        foreach (var assembly in Assemblies.All.Where(candidate => candidate != Assemblies.GoogleSheets))
        {
            References.AssertNoModuleReference(assembly, Assemblies.GoogleApis);
        }
    }

    [Fact]
    public void GoogleSheetsInfrastructureActuallyCarriesGoogleApis()
    {
        References.AssertModuleReference(Assemblies.GoogleSheets, Assemblies.GoogleApis);
    }

    [Fact]
    public void TheTwoAdaptersDoNotReferenceEachOther()
    {
        References.AssertNoModuleReference(Assemblies.GoogleSheets, Assemblies.ConfigurationNamespace);
        References.AssertNoModuleReference(Assemblies.Configuration, Assemblies.GoogleSheetsNamespace);
    }

    [Fact]
    public void NoCoreOrAdapterLayerReferencesAHostAssembly()
    {
        Assembly[] cores = [Assemblies.Domain, Assemblies.Application, Assemblies.GoogleSheets, Assemblies.Configuration];

        foreach (var assembly in cores)
        {
            References.AssertNoModuleReference(assembly, Assemblies.WebNamespace, Assemblies.CliAssemblyName);
        }
    }

    [Fact]
    public void EveryAssemblyUnderTestIsResolvable()
    {
        string[] expected =
        [
            Assemblies.CliAssemblyName,
            Assemblies.ApplicationNamespace,
            Assemblies.ConfigurationNamespace,
            Assemblies.GoogleSheetsNamespace,
            Assemblies.DomainNamespace,
            Assemblies.WebNamespace,
        ];

        Assert.Equal(
            expected.OrderBy(name => name, StringComparer.Ordinal),
            Assemblies.All.Select(assembly => assembly.GetName().Name!).OrderBy(name => name, StringComparer.Ordinal));
    }

    private static void AssertNoTypeDependency(Assembly assembly, string forbidden)
    {
        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        var offenders = result.FailingTypeNames is { } names
            ? string.Join(", ", names.OrderBy(name => name, StringComparer.Ordinal))
            : "<none>";

        Assert.True(
            result.IsSuccessful,
            $"'{assembly.GetName().Name}' must not depend on '{forbidden}': outer layers may depend on inner " +
            $"ones, never the reverse, and adapter types must not leak into the core. Offending types: {offenders}.");
    }
}
