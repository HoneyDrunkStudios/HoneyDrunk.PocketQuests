namespace PocketQuests.Domain.Models.Progress;

/// <summary>Current breadth rank and progress toward the next rank's two gates.</summary>
/// <param name="Current">The current value or state for this result.</param>
/// <param name="Requirement">The requirement evaluated by this operation.</param>
/// <param name="QualifyingCategories">The categories that satisfy the requirement.</param>
/// <param name="Total">The total value represented by this result.</param>
public record RankProgress(Rank Current, RankRule Requirement, int QualifyingCategories, long Total);
