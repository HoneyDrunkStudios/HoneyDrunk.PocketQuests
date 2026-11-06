namespace PocketQuests.Domain.Models.Quests;

/// <summary>An account-owned editable definition; accepted and completed revisions are separate snapshots.</summary>
/// <param name="Quest">The quest associated with this operation.</param>
/// <param name="Revision">The immutable resource revision.</param>
/// <param name="Archived">Whether the selected definition is archived.</param>
public record QuestDefinition(Quest Quest, int Revision = 1, bool Archived = false);
