using PocketQuests.Contracts.Models.Catalogs;
using PocketQuests.Contracts.Models.Progress;
using PocketQuests.Contracts.Models.Quests;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Catalogs;

/// <summary>The public quest catalog and authoritative global rank rules.</summary>
/// <param name="Categories">The categories included in this snapshot.</param>
/// <param name="Attributes">The character attributes in this snapshot.</param>
/// <param name="Skills">The skills included in this snapshot.</param>
/// <param name="Quests">The quests included in this snapshot.</param>
/// <param name="Rules">The rules evaluated by this operation.</param>
public sealed record CatalogResponse(
    ImmutableArray<NamedItem> Categories,
    ImmutableArray<NamedItem> Attributes,
    ImmutableArray<NamedItem> Skills,
    ImmutableArray<Quest> Quests,
    ImmutableArray<RankRule> Rules);
