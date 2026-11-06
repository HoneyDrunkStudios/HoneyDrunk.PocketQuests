using PocketQuests.Contracts.Requests.Commands;

namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>A stable client-generated draft identity and optimistic catalog version; null version creates a draft.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Quest">The quest associated with this operation.</param>
/// <param name="ExpectedVersion">The version required by optimistic concurrency validation.</param>
public sealed record SaveSystemQuestRequest(Guid Id, QuestInput Quest, string? ExpectedVersion = null);
