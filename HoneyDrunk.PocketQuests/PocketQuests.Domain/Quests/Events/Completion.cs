using PocketQuests.Domain.Quests.Definitions;

namespace PocketQuests.Domain.Quests.Events;

/// <summary>An immutable server-timed completion event for an accepted occurrence.</summary>
public record Completion(Guid Id, Guid OccurrenceId, DateTimeOffset RecordedAt, Quest? Snapshot = null);
