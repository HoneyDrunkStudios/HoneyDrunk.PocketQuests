namespace PocketQuests.Domain.Models.Progress;

/// <summary>Required category count, per-category floor and total category XP for a rank.</summary>
/// <param name="Rank">The quest or progression rank.</param>
/// <param name="Count">The number of matching items.</param>
/// <param name="Floor">The minimum value used by this calculation.</param>
/// <param name="Total">The total value represented by this result.</param>
public record RankRule(Rank Rank, int Count, long Floor, long Total);
