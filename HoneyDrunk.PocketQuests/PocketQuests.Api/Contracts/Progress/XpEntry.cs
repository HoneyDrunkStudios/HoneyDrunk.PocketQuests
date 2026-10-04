using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Progress;

/// <summary>The public XpEntry JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record XpEntry(Guid EventId, Guid OccurrenceId, DateTimeOffset At, string Track, string TrackId, long Amount);
