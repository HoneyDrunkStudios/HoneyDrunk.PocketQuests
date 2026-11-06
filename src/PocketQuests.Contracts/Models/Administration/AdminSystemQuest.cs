using PocketQuests.Contracts.Models.Quests;

namespace PocketQuests.Contracts.Models.Administration;

/// <summary>Saved studio draft and separate published terms, with an optimistic head version.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Version">The version of the represented state or resource.</param>
/// <param name="DraftNumber">The draft sequence number.</param>
/// <param name="DraftSystemQuestRevisionId">The saved system quest draft revision identifier.</param>
/// <param name="PublishedSystemQuestRevisionId">The published system quest revision identifier.</param>
/// <param name="Draft">The saved draft included in this result.</param>
/// <param name="Published">Whether the system quest is published for discovery.</param>
/// <param name="ModifiedAt">The most recent modification timestamp.</param>
public sealed record AdminSystemQuest(
    Guid Id,
    string Version,
    int DraftNumber,
    Guid DraftSystemQuestRevisionId,
    Guid? PublishedSystemQuestRevisionId,
    Quest Draft,
    Quest? Published,
    DateTimeOffset ModifiedAt);
