namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public XpEntry JSON contract, independent of storage and domain behavior.</summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="At">The instant associated with this event or calculation.</param>
/// <param name="Track">The progression track associated with this operation.</param>
/// <param name="TrackId">The progression track identifier.</param>
/// <param name="Amount">The amount applied by this operation.</param>
public sealed record XpEntry(Guid EventId, Guid OccurrenceId, DateTimeOffset At, string Track, string TrackId, long Amount);
