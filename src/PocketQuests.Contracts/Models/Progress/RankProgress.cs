using PocketQuests.Contracts.Enums.Progress;

namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public RankProgress JSON contract, independent of storage and domain behavior.</summary>
public sealed record RankProgress(Rank Current, RankRule Requirement, int QualifyingCategories, long Total);
