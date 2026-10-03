using PocketQuests.Domain.Quests.Definitions;

namespace PocketQuests.Domain.Quests.Occurrences;

/// <summary>The accepted quest terms and absolute deadline captured at acceptance.</summary>
public record Occurrence(Guid Id, Quest Quest, string? DueDate, DateTimeOffset? Deadline, DateTimeOffset AcceptedAt, string? PlannedTime = null, Guid? ParentId = null, OccurrenceLifecycle? Lifecycle = null);
