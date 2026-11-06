namespace PocketQuests.Domain.Models.Schedules;

/// <summary>A proposed deadline and whether applying it now would miss an active occurrence.</summary>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="Title">The human-readable title.</param>
/// <param name="Deadline">The occurrence deadline.</param>
/// <param name="BecomesMissed">Whether this transition marks the occurrence missed.</param>
public record DeadlineChange(Guid OccurrenceId, string Title, DateTimeOffset Deadline, bool BecomesMissed);
