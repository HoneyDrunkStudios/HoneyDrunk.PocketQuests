namespace PocketQuests.Domain.Models.Quests;

/// <summary>An immutable server-timed completion event for an accepted occurrence.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="RecordedAt">The retained timestamp for this event.</param>
/// <param name="Snapshot">The immutable state snapshot included in this result.</param>
public record Completion(Guid Id, Guid OccurrenceId, DateTimeOffset RecordedAt, Quest? Snapshot = null);
