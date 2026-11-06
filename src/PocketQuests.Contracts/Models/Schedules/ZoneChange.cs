namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public ZoneChange JSON contract, independent of storage and domain behavior.</summary>
/// <param name="From">The beginning of the represented range or transition.</param>
/// <param name="To">The end of the represented range or transition.</param>
/// <param name="At">The instant associated with this event or calculation.</param>
public sealed record ZoneChange(string From, string To, DateTimeOffset At);
