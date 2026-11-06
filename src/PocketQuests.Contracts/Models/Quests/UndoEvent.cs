namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public UndoEvent JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="CompletionId">The completion identifier.</param>
/// <param name="RecordedAt">The retained timestamp for this event.</param>
public sealed record UndoEvent(Guid Id, Guid CompletionId, DateTimeOffset RecordedAt);
