using PocketQuests.Contracts.Models.Quests;

namespace PocketQuests.Contracts.Models.Administration;

/// <summary>Saved studio draft and separate published terms, with an optimistic head version.</summary>
public sealed record AdminSystemQuest(Guid Id, string Version, int DraftNumber, Guid DraftSystemQuestRevisionId,
    Guid? PublishedSystemQuestRevisionId, Quest Draft, Quest? Published, DateTimeOffset ModifiedAt);
