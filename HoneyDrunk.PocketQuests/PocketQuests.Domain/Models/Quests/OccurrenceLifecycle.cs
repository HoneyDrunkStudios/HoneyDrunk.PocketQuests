namespace PocketQuests.Domain.Models.Quests;

/// <summary>Durable occurrence scheduling and explicit freeze state.</summary>
public record OccurrenceLifecycle(Guid? SeriesId = null, int? Sequence = null, int? ScheduleVersion = null,
    DateTimeOffset? FrozenAt = null, bool IndividuallyFrozen = false, DateTimeOffset? AbandonedAt = null, int? LockedLoss = null, string? LossCategoryId = null, bool Unaccepted = false, Guid? SourceAnchorId = null, string? DeadlineZone = null);
