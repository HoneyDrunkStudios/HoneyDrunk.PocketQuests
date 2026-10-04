namespace PocketQuests.Domain.Models.Progress;

/// <summary>Recomputed eligibility for a catalog achievement, badge or frame.</summary>
public record Entitlement(string Id, string Kind, string Name, int Count, int RequiredCount, Rank RequiredRank, bool Earned);
