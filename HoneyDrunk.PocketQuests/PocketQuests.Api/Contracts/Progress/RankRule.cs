using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Progress;

/// <summary>The public RankRule JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record RankRule(Rank Rank, int Count, long Floor, long Total);
