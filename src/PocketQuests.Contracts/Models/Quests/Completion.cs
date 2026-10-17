namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public Completion JSON contract, independent of storage and domain behavior.</summary>
public sealed record Completion(Guid Id, Guid OccurrenceId, DateTimeOffset RecordedAt, Quest? Snapshot);
