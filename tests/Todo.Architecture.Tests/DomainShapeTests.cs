using System.Reflection;
using NetArchTest.Rules;
using Todo.Domain.Spaces;
using Todo.Domain.Tasks;

namespace Todo.Architecture.Tests;

/// <summary>Shape rules that keep the domain a closed, immutable model.</summary>
public sealed class DomainShapeTests
{
    [Fact]
    public void EveryNonAbstractDomainClassIsSealed()
    {
        var result = Types.InAssembly(Assemblies.Domain)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed()
            .GetResult();

        var offenders = result.FailingTypeNames is { } names
            ? string.Join(", ", names.OrderBy(name => name, StringComparer.Ordinal))
            : "<none>";

        Assert.True(
            result.IsSuccessful,
            "Every non-abstract domain class must be sealed so the model cannot be extended from outside the " +
            $"assembly. Offending types: {offenders}.");
    }

    [Fact]
    public void DomainTypesStayInTheDocumentedNamespaces()
    {
        string[] allowed = ["Todo.Domain", "Todo.Domain.Common", "Todo.Domain.Tasks", "Todo.Domain.Spaces", "Todo.Domain.Notes"];

        var unexpected = DeclaredTypes()
            .Where(type => !allowed.Contains(type.Namespace ?? string.Empty, StringComparer.Ordinal))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unexpected.Length == 0,
            $"Domain types may only live in [{string.Join(", ", allowed)}] but found [{string.Join(", ", unexpected)}].");
    }

    [Fact]
    public void DomainValueObjectsAreValueTypesSoTheyCannotBeNull()
    {
        Type[] valueObjects = [typeof(TaskId), typeof(DueDate), typeof(SpaceName)];

        Assert.All(valueObjects, type => Assert.True(type.IsValueType, $"{type.Name} must be a value type."));
    }

    [Fact]
    public void DomainEntitiesAndErrorTypesAreRecordsWithStructuralEquality()
    {
        Type[] records = [typeof(TaskItem), typeof(Todo.Domain.Notes.Note), typeof(Todo.Domain.Common.FieldErrors)];

        Assert.All(
            records,
            type => Assert.True(
                type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null,
                $"{type.Name} must be a record so equality is structural."));
    }

    [Fact]
    public void TheDomainDeclaresNoMutablePublicStaticFields()
    {
        var mutable = DeclaredTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => !field.IsInitOnly && !field.IsLiteral)
            .Select(field => $"{field.DeclaringType?.Name}.{field.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            mutable.Length == 0,
            $"Public mutable static fields are process-wide state and must not exist in the domain: [{string.Join(", ", mutable)}].");
    }

    /// <summary>
    /// <see cref="Assembly.GetTypes"/> also returns compiler-generated closure classes, lambda caches
    /// and <c>&lt;PrivateImplementationDetails&gt;</c> array blobs whose shape is not part of the
    /// hand-written contract, so they are filtered out.
    /// </summary>
    private static IEnumerable<Type> DeclaredTypes() =>
        Assemblies.Domain.GetTypes().Where(type => !IsCompilerGenerated(type));

    private static bool IsCompilerGenerated(Type type) =>
        type.Name.StartsWith('<')
        || (type.FullName?.Contains('<', StringComparison.Ordinal) ?? false)
        || type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);
}
