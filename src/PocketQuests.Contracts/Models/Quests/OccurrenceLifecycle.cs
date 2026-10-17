namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public OccurrenceLifecycle JSON contract, independent of storage and domain behavior.</summary>
public sealed record OccurrenceLifecycle(Guid? SeriesId, int? Sequence, int? ScheduleVersion,
    DateTimeOffset? FrozenAt, bool IndividuallyFrozen, DateTimeOffset? AbandonedAt, int? LockedLoss, string? LossCategoryId, bool Unaccepted, Guid? SourceAnchorId, string? DeadlineZone);
