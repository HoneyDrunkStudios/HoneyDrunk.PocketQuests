namespace PocketQuests.Domain.Models.Schedules;

/// <summary>A selected-zone transition; its date jump contributes no elapsed activity or freeze days.</summary>
public record ZoneChange(string From, string To, DateTimeOffset At);
