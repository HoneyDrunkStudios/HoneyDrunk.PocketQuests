using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Queries.Administration;

/// <summary>Selected immutable terms and the current catalog head, without account content.</summary>
public sealed record SystemQuestStudioRow(SystemQuestEntity Head, SystemQuestRevisionEntity Draft, SystemQuestRevisionEntity? Published);
