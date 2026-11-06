namespace PocketQuests.Contracts.Requests.Discoveries;

/// <summary>Removes only the observed revision of an account-owned saved place.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="ExpectedRevision">The revision required by optimistic concurrency validation.</param>
public sealed record RemovePlaceRequest(Guid Id, int ExpectedRevision);
