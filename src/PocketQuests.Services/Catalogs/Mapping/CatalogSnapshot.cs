using PocketQuests.Contracts.Responses.Catalogs;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Progress;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Catalogs.Mapping;

/// <summary>Pure built-in catalog mapping shared by runtime and wire fixtures.</summary>
public static class CatalogSnapshot
{
    /// <summary>Maps the source-controlled starter content and reward rules.</summary>
    /// <returns>The built-in catalog snapshot.</returns>
    public static CatalogResponse BuiltIn() => new(
        [.. Catalog.Categories.Select(item => item.ToModel())],
        [.. Catalog.Attributes.Select(item => item.ToModel())],
        [.. Catalog.Skills.Select(item => item.ToModel())],
        [.. Catalog.Quests.Select(item => item.ToModel())],
        [.. Progression.Rules.Select(item => item.ToModel())]);
}
