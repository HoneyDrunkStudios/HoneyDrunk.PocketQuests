using PocketQuests.Contracts.Requests.Commands;

namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>A stable client-generated draft identity and optimistic catalog version; null version creates a draft.</summary>
public sealed record SaveSystemQuestRequest(Guid Id, QuestInput Quest, string? ExpectedVersion = null);
