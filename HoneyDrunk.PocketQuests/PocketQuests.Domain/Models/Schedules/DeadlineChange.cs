namespace PocketQuests.Domain.Models.Schedules;

/// <summary>A proposed deadline and whether applying it now would miss an active occurrence.</summary>
public record DeadlineChange(Guid OccurrenceId, string Title, DateTimeOffset Deadline, bool BecomesMissed);
