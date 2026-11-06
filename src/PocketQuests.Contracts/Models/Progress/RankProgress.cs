using PocketQuests.Contracts.Enums.Progress;

namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public RankProgress JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Current">The current value or state for this result.</param>
/// <param name="Requirement">The requirement evaluated by this operation.</param>
/// <param name="QualifyingCategories">The categories that satisfy the requirement.</param>
/// <param name="Total">The total value represented by this result.</param>
public sealed record RankProgress(Rank Current, RankRule Requirement, int QualifyingCategories, long Total);
