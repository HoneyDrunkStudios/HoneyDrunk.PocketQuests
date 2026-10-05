using PocketQuests.Contracts.Enums.Progress;

namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public RankRule JSON contract, independent of storage and domain behavior.</summary>
public sealed record RankRule(Rank Rank, int Count, long Floor, long Total);
