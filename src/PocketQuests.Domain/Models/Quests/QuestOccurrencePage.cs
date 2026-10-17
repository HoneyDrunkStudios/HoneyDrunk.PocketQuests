using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>A bounded page over committed occurrence rows in original creation order.</summary>
/// <param name="Items">At most the requested number of occurrence views.</param>
/// <param name="NextAfter">Exclusive creation cursor for the next page, or null at the end.</param>
/// <param name="MutationVersion">Account version consistently observed by this page.</param>
/// <param name="ProjectionAsOfAt">Clock through which committed occurrence delivery was reconciled; callers can identify stale materialization.</param>
/// <param name="HasPendingReconciliation">Whether another bounded delivery step is needed before materialization reaches its requested clock.</param>
public sealed record QuestOccurrencePage(ImmutableArray<OccurrenceView> Items, int? NextAfter, long MutationVersion, DateTimeOffset ProjectionAsOfAt, bool HasPendingReconciliation);
