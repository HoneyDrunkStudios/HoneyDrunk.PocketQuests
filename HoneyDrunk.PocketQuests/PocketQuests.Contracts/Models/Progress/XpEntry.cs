namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public XpEntry JSON contract, independent of storage and domain behavior.</summary>
public sealed record XpEntry(Guid EventId, Guid OccurrenceId, DateTimeOffset At, string Track, string TrackId, long Amount);
