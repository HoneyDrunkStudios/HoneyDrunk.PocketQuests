namespace PocketQuests.Domain.Models.Progress;

/// <summary>Recomputed eligibility for a catalog achievement, badge or frame.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Kind">The event or requirement classification.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Count">The number of matching items.</param>
/// <param name="RequiredCount">The count required to satisfy the requirement.</param>
/// <param name="RequiredRank">The rank required to satisfy the requirement.</param>
/// <param name="Earned">The experience earned by this calculation.</param>
public record Entitlement(string Id, string Kind, string Name, int Count, int RequiredCount, Rank RequiredRank, bool Earned);
