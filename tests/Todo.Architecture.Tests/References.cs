using System.Reflection;

namespace Todo.Architecture.Tests;

/// <summary>
/// Surfaces the project-to-project and package dependencies that a per-type rule cannot see, for
/// example when an assembly declares no types at all.
/// </summary>
internal static class References
{
    public static string[] ReferencedNames(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];

    public static void AssertNoModuleReference(Assembly assembly, params string[] forbidden)
    {
        var offenders = ReferencedNames(assembly)
            .Where(name => MatchesAny(name, forbidden))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"'{assembly.GetName().Name}' must not reference [{string.Join(", ", forbidden)}] at the assembly " +
            $"level, but its module references [{string.Join(", ", offenders)}].");
    }

    public static void AssertModuleReference(Assembly assembly, params string[] expected)
    {
        var referenced = ReferencedNames(assembly);

        foreach (var name in expected)
        {
            Assert.True(
                referenced.Any(candidate => MatchesAny(candidate, [name])),
                $"'{assembly.GetName().Name}' is expected to reference '{name}' but does not. " +
                $"Actual references: [{string.Join(", ", referenced.OrderBy(value => value, StringComparer.Ordinal))}].");
        }
    }

    /// <summary>
    /// A pattern matches the assembly itself or a child assembly, so "Google.Apis" catches
    /// "Google.Apis", "Google.Apis.Auth" and "Google.Apis.Sheets.v4" alike.
    /// </summary>
    private static bool MatchesAny(string assemblyName, IEnumerable<string> patterns) =>
        patterns.Any(pattern => assemblyName.Equals(pattern, StringComparison.Ordinal)
            || assemblyName.StartsWith(pattern + ".", StringComparison.Ordinal));
}
