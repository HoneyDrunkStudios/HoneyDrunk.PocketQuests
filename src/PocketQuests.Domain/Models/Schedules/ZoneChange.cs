namespace PocketQuests.Domain.Models.Schedules;

/// <summary>A selected-zone transition; its date jump contributes no elapsed activity or freeze days.</summary>
/// <param name="From">The beginning of the represented range or transition.</param>
/// <param name="To">The end of the represented range or transition.</param>
/// <param name="At">The instant associated with this event or calculation.</param>
public record ZoneChange(string From, string To, DateTimeOffset At);
