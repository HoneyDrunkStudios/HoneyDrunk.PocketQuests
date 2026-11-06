namespace PocketQuests.Domain.Models.Quests;

/// <summary>An append-only reversal targeting exactly one completion event.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="CompletionId">The completion identifier.</param>
/// <param name="RecordedAt">The retained timestamp for this event.</param>
public record UndoEvent(Guid Id, Guid CompletionId, DateTimeOffset RecordedAt);
