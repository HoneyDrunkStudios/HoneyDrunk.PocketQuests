using PocketQuests.Contracts.Responses.Catalogs;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Progress;
using PocketQuests.Services.Catalogs.Mapping;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Catalogs;

/// <summary>Maps the existing pure catalog without unnecessary persistence reads.</summary>
public sealed class CatalogService : ICatalogService
{
    /// <inheritdoc />
    public CatalogResponse Read() => new(
        [.. Catalog.Categories.Select(item => item.ToModel())],
        [.. Catalog.Attributes.Select(item => item.ToModel())],
        [.. Catalog.Skills.Select(item => item.ToModel())],
        [.. Catalog.Quests.Select(item => item.ToModel())],
        [.. Progression.Rules.Select(item => item.ToModel())]);
}
