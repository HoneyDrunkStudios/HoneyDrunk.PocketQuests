namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>Review of one saved draft and head version, to publish or withdraw future discovery.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="ExpectedVersion">The version required by optimistic concurrency validation.</param>
/// <param name="ExpectedDraftRevisionId">The draft revision the caller expects to modify.</param>
/// <param name="Published">Whether the system quest is published for discovery.</param>
public sealed record PublishSystemQuestRequest(Guid Id, string ExpectedVersion, Guid ExpectedDraftRevisionId, bool Published);
