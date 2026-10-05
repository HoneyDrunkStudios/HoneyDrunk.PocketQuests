namespace PocketQuests.Domain.Models.Quests;

/// <summary>An immutable server-timed completion event for an accepted occurrence.</summary>
public record Completion(Guid Id, Guid OccurrenceId, DateTimeOffset RecordedAt, Quest? Snapshot = null);
