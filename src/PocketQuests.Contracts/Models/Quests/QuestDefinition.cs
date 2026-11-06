namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public QuestDefinition JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Quest">The quest associated with this operation.</param>
/// <param name="Revision">The immutable resource revision.</param>
/// <param name="Archived">Whether the selected definition is archived.</param>
public sealed record QuestDefinition(Quest Quest, int Revision, bool Archived);
