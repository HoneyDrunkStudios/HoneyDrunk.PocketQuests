using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Quests.Occurrences;

/// <summary>The public OccurrenceLifecycle JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record OccurrenceLifecycle(Guid? SeriesId, int? Sequence, int? ScheduleVersion,
    DateTimeOffset? FrozenAt, bool IndividuallyFrozen, DateTimeOffset? AbandonedAt, int? LockedLoss, string? LossCategoryId, bool Unaccepted, Guid? SourceAnchorId, string? DeadlineZone);
