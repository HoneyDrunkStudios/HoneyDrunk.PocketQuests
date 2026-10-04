namespace PocketQuests.Domain.Models.Quests;

/// <summary>Private maintenance cursor over the indexed committed materialization clock and account UUID.</summary>
/// <param name="ProjectionAsOfAt">Clock observed when the account was selected.</param>
/// <param name="AccountId">Tie-breaker in SQL's UUID ordering.</param>
public sealed record ReconciliationCursor(DateTimeOffset ProjectionAsOfAt, Guid AccountId);
