using Microsoft.EntityFrameworkCore;
using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.SchemaTests.Architecture;

/// <summary>Five concrete product boundaries; composition-root persistence registration remains legal.</summary>
internal static class ArchitectureRules
{
    internal static bool IsPersistence(Type type) => typeof(DbContext).IsAssignableFrom(type)
        || InNamespace(type, "Microsoft.EntityFrameworkCore")
        || InNamespace(type, "HoneyDrunk.Data.EntityFramework")
        || (type.BaseType is not null && IsPersistence(type.BaseType))
        || type.GetInterfaces().Any(IsPersistence);

    internal static bool IsData(Type type) => InNamespace(type, "PocketQuests.Data") || InNamespace(type, "HoneyDrunk.Data") || IsPersistence(type);

    internal static bool IsContract(Type type) => InNamespace(type, "PocketQuests.Contracts");

    internal static bool BypassesService(Type type) => IsData(type) || InNamespace(type, "PocketQuests.Domain");

    internal static bool IsImpureDomainDependency(Type type) =>
        !type.IsGenericParameter && type.Assembly != typeof(QuestAggregate).Assembly
        && (!(InNamespace(type, "NodaTime") || InNamespace(type, "System"))
        || IsHostIo(type) || InNamespace(type, "System.Net") || InNamespace(type, "System.Reflection")
        || type == typeof(TimeProvider) || type == typeof(Environment) || type == typeof(System.Diagnostics.Process));

    internal static bool InNamespace(Type type, string name) => type.Namespace == name
        || type.Namespace?.StartsWith(name + ".", StringComparison.Ordinal) == true;

    internal static string[] Violations(IEnumerable<Type> types, Func<Type, bool> forbidden) => [.. types
        .SelectMany(source => TypeDependencies.Read(source)
            .Where(dependency => dependency != source && forbidden(dependency))
            .Select(dependency => $"{source.FullName} -> {dependency.FullName}"))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    // CommandDigest uses deterministic in-memory serialization, not host file access.
    private static bool IsHostIo(Type type) => InNamespace(type, "System.IO")
        && type != typeof(Stream) && type != typeof(MemoryStream) && type != typeof(SeekOrigin);
}
