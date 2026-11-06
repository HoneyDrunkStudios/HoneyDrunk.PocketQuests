using PocketQuests.Contracts.Models.Quests;

namespace PocketQuests.Contracts.Models.Administration;

/// <summary>One retained content revision, without operator account identifiers.</summary>
/// <param name="Id">The immutable content revision identifier.</param>
/// <param name="Number">The content revision number.</param>
/// <param name="CreatedAt">The revision creation timestamp.</param>
/// <param name="Quest">The retained immutable quest terms.</param>
public sealed record AdminSystemQuestRevision(Guid Id, int Number, DateTimeOffset CreatedAt, Quest Quest);
