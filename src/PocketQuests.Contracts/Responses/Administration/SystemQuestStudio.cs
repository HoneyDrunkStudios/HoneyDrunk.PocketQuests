using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Responses.Catalogs;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Administration;

/// <summary>Bounded authored templates and code-owned starter choices for editing and preview.</summary>
public sealed record SystemQuestStudio(ImmutableArray<AdminSystemQuest> Quests, CatalogResponse Catalog);
