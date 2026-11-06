using PocketQuests.Contracts.Models.Administration;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Administration;

/// <summary>A newest-first bounded page of immutable content; NextBefore is an exclusive version cursor.</summary>
/// <param name="Id">The system quest identifier.</param>
/// <param name="Revisions">The newest-first bounded page of retained revisions.</param>
/// <param name="NextBefore">The exclusive cursor for the next page, when present.</param>
public sealed record SystemQuestHistory(Guid Id, ImmutableArray<AdminSystemQuestRevision> Revisions, int? NextBefore);
