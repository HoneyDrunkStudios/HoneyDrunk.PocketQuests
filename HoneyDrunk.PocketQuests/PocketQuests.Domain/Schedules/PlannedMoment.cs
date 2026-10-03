namespace PocketQuests.Domain.Schedules;

/// <summary>The resolved first occurrence of a planned clock time, with any gap adjustment disclosed.</summary>
public record PlannedMoment(string Requested, string Resolved, DateTimeOffset Instant, bool Adjusted, bool Repeated);
