using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Queries.Quests;

/// <summary>One owned current head and its retained revision; a missing revision remains visible to validation.</summary>
/// <param name="Head">The tracked current entity.</param>
/// <param name="Revision">Its retained revision, or null for inconsistent source data.</param>
public sealed record CurrentQuestDefinition(QuestDefinitionEntity Head, QuestDefinitionRevisionEntity? Revision);
