namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public OccurrenceLifecycle JSON contract, independent of storage and domain behavior.</summary>
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
public sealed record OccurrenceLifecycle(
    Guid? SeriesId,
    int? Sequence,
    int? ScheduleVersion,
    DateTimeOffset? FrozenAt,
    bool IndividuallyFrozen,
    DateTimeOffset? AbandonedAt,
    int? LockedLoss,
    string? LossCategoryId,
    bool Unaccepted,
    Guid? SourceAnchorId,
    string? DeadlineZone);
