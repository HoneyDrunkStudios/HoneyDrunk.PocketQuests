using PocketQuests.Contracts.Models.Quests;

namespace PocketQuests.Contracts.Models.Administration;

/// <summary>One retained content revision, without operator account identifiers.</summary>
public sealed record AdminSystemQuestRevision(Guid Id, int Number, DateTimeOffset CreatedAt, Quest Quest);
