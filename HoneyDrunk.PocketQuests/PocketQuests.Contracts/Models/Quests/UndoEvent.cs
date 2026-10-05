namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public UndoEvent JSON contract, independent of storage and domain behavior.</summary>
public sealed record UndoEvent(Guid Id, Guid CompletionId, DateTimeOffset RecordedAt);
