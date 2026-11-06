namespace PocketQuests.Domain.Models.Schedules;

/// <summary>The resolved first occurrence of a planned clock time, with any gap adjustment disclosed.</summary>
/// <param name="Requested">The value requested by the caller.</param>
/// <param name="Resolved">The resolved value or state.</param>
/// <param name="Instant">The resolved instant.</param>
/// <param name="Adjusted">Whether the requested value was adjusted.</param>
/// <param name="Repeated">Whether this operation repeats an already recorded command.</param>
public record PlannedMoment(string Requested, string Resolved, DateTimeOffset Instant, bool Adjusted, bool Repeated);
