using PocketQuests.Contracts.Models.Catalogs;
using PocketQuests.Contracts.Models.Progress;
using PocketQuests.Contracts.Models.Quests;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Catalogs;

/// <summary>The public quest catalog and authoritative global rank rules.</summary>
public sealed record CatalogResponse(ImmutableArray<NamedItem> Categories, ImmutableArray<NamedItem> Attributes,
    ImmutableArray<NamedItem> Skills, ImmutableArray<Quest> Quests, ImmutableArray<RankRule> Rules);
