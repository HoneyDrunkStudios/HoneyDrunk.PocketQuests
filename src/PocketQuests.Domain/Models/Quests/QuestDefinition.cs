namespace PocketQuests.Domain.Models.Quests;

/// <summary>An account-owned editable definition; accepted and completed revisions are separate snapshots.</summary>
public record QuestDefinition(Quest Quest, int Revision = 1, bool Archived = false);
