namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>Review of one saved draft and head version, to publish or withdraw future discovery.</summary>
public sealed record PublishSystemQuestRequest(Guid Id, string ExpectedVersion, Guid ExpectedDraftRevisionId, bool Published);
