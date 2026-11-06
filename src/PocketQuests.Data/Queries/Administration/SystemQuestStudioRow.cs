using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Queries.Administration;

/// <summary>Selected immutable terms and the current catalog head, without account content.</summary>
/// <param name="Head">The current resource head included in this result.</param>
/// <param name="Draft">The saved draft included in this result.</param>
/// <param name="Published">Whether the system quest is published for discovery.</param>
public sealed record SystemQuestStudioRow(SystemQuestEntity Head, SystemQuestRevisionEntity Draft, SystemQuestRevisionEntity? Published);
