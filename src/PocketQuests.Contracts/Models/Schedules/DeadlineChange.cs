namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public DeadlineChange JSON contract, independent of storage and domain behavior.</summary>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="Title">The human-readable title.</param>
/// <param name="Deadline">The occurrence deadline.</param>
/// <param name="BecomesMissed">Whether this transition marks the occurrence missed.</param>
public sealed record DeadlineChange(Guid OccurrenceId, string Title, DateTimeOffset Deadline, bool BecomesMissed);
