namespace PocketQuests.Domain.Models.Quests;

/// <summary>Durable occurrence scheduling and explicit freeze state.</summary>
/// <param name="SeriesId">The recurring quest series identifier.</param>
/// <param name="Sequence">The event sequence number.</param>
/// <param name="ScheduleVersion">The version of the recurrence schedule.</param>
/// <param name="FrozenAt">The instant the accepted terms were frozen.</param>
/// <param name="IndividuallyFrozen">Whether this occurrence has individually frozen terms.</param>
/// <param name="AbandonedAt">The instant the occurrence was abandoned.</param>
/// <param name="LockedLoss">The loss retained in the accepted terms.</param>
/// <param name="LossCategoryId">The category to which the loss applies.</param>
/// <param name="Unaccepted">Whether the occurrence remains unaccepted.</param>
/// <param name="SourceAnchorId">The source recorded-clock anchor identifier.</param>
/// <param name="DeadlineZone">The time zone used to interpret the deadline.</param>
public record OccurrenceLifecycle(
    Guid? SeriesId = null,
    int? Sequence = null,
    int? ScheduleVersion = null,
    DateTimeOffset? FrozenAt = null,
    bool IndividuallyFrozen = false,
    DateTimeOffset? AbandonedAt = null,
    int? LockedLoss = null,
    string? LossCategoryId = null,
    bool Unaccepted = false,
    Guid? SourceAnchorId = null,
    string? DeadlineZone = null);
