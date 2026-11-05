namespace PocketQuests.Contracts.Requests.Discoveries;

/// <summary>Removes only the observed revision of an account-owned saved place.</summary>
public sealed record RemovePlaceRequest(Guid Id, int ExpectedRevision);
