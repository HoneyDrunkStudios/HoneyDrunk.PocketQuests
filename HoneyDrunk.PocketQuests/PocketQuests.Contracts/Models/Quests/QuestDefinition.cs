namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public QuestDefinition JSON contract, independent of storage and domain behavior.</summary>
public sealed record QuestDefinition(Quest Quest, int Revision, bool Archived);
