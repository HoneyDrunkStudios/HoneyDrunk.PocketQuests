namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public Occurrence JSON contract, independent of storage and domain behavior.</summary>
public sealed record Occurrence(Guid Id, Quest Quest, string? DueDate, DateTimeOffset? Deadline, DateTimeOffset AcceptedAt, string? PlannedTime, Guid? ParentId, OccurrenceLifecycle? Lifecycle);
