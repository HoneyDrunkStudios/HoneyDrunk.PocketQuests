namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public Completion JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="RecordedAt">The retained timestamp for this event.</param>
/// <param name="Snapshot">The immutable state snapshot included in this result.</param>
public sealed record Completion(Guid Id, Guid OccurrenceId, DateTimeOffset RecordedAt, Quest? Snapshot);
