using PocketQuests.Contracts.Enums.Progress;

namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public RankRule JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Rank">The quest or progression rank.</param>
/// <param name="Count">The number of matching items.</param>
/// <param name="Floor">The minimum value used by this calculation.</param>
/// <param name="Total">The total value represented by this result.</param>
public sealed record RankRule(Rank Rank, int Count, long Floor, long Total);
