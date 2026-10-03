namespace PocketQuests.Domain.Progress;

/// <summary>Required category count, per-category floor and total category XP for a rank.</summary>
public record RankRule(Rank Rank, int Count, long Floor, long Total);
