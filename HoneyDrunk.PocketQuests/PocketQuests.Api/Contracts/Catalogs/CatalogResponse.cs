using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Quests.Definitions;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Catalogs;

/// <summary>The public quest catalog and authoritative global rank rules.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record CatalogResponse(ImmutableArray<NamedItem> Categories, ImmutableArray<NamedItem> Attributes,
    ImmutableArray<NamedItem> Skills, ImmutableArray<Quest> Quests, ImmutableArray<RankRule> Rules);
