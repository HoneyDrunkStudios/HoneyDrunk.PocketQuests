namespace PocketQuests.Domain.Models.Progress;

/// <summary>Current breadth rank and progress toward the next rank's two gates.</summary>
public record RankProgress(Rank Current, RankRule Requirement, int QualifyingCategories, long Total);
