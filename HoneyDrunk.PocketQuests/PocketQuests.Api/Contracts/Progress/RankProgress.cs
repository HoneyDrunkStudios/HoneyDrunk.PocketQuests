using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Progress;

/// <summary>The public RankProgress JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record RankProgress(Rank Current, RankRule Requirement, int QualifyingCategories, long Total);
