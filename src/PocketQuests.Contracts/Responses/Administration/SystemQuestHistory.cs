using PocketQuests.Contracts.Models.Administration;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Administration;

/// <summary>A newest-first bounded page of immutable content; NextBefore is an exclusive version cursor.</summary>
public sealed record SystemQuestHistory(Guid Id, ImmutableArray<AdminSystemQuestRevision> Revisions, int? NextBefore);
