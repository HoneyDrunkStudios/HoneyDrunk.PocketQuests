namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public PlannedMoment JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Requested">The value requested by the caller.</param>
/// <param name="Resolved">The resolved value or state.</param>
/// <param name="Instant">The resolved instant.</param>
/// <param name="Adjusted">Whether the requested value was adjusted.</param>
/// <param name="Repeated">Whether this operation repeats an already recorded command.</param>
public sealed record PlannedMoment(string Requested, string Resolved, DateTimeOffset Instant, bool Adjusted, bool Repeated);
