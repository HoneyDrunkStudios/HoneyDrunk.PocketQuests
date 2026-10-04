namespace PocketQuests.Domain.Models.Quests;

/// <summary>An append-only reversal targeting exactly one completion event.</summary>
public record UndoEvent(Guid Id, Guid CompletionId, DateTimeOffset RecordedAt);
