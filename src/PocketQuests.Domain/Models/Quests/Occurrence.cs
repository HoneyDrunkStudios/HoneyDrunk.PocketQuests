namespace PocketQuests.Domain.Models.Quests;

/// <summary>The accepted quest terms and absolute deadline captured at acceptance.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Quest">The quest associated with this operation.</param>
/// <param name="DueDate">The due date interpreted in the applicable account time zone.</param>
/// <param name="Deadline">The occurrence deadline.</param>
/// <param name="AcceptedAt">The instant the offered quest was accepted.</param>
/// <param name="PlannedTime">The planned occurrence time.</param>
/// <param name="ParentId">The parent resource identifier, when present.</param>
/// <param name="Lifecycle">The account lifecycle state included in this result.</param>
public record Occurrence(Guid Id, Quest Quest, string? DueDate, DateTimeOffset? Deadline, DateTimeOffset AcceptedAt, string? PlannedTime = null, Guid? ParentId = null, OccurrenceLifecycle? Lifecycle = null);
