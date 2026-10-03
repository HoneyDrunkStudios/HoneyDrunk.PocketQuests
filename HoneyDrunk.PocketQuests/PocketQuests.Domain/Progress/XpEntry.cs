namespace PocketQuests.Domain.Progress;

/// <summary>A reproducible earned or assessed entry; experience seeds remain a separate component.</summary>
public record XpEntry(Guid EventId, Guid OccurrenceId, DateTimeOffset At, string Track, string TrackId, long Amount);
