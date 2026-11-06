using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Responses.Catalogs;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Administration;

/// <summary>Bounded authored templates and code-owned starter choices for editing and preview.</summary>
/// <param name="Quests">The quests included in this snapshot.</param>
/// <param name="Catalog">The catalog used by this operation.</param>
public sealed record SystemQuestStudio(ImmutableArray<AdminSystemQuest> Quests, CatalogResponse Catalog);
