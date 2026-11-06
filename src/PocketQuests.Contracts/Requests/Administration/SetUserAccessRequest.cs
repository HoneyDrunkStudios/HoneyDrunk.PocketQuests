namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>An explicit product suspension or reactivation with stale-edit protection and a private reason.</summary>
/// <param name="UserId">The canonical account user identifier.</param>
/// <param name="Suspended">Whether account access is suspended.</param>
/// <param name="Reason">The human-readable reason associated with this result.</param>
/// <param name="ExpectedRevision">The revision required by optimistic concurrency validation.</param>
public sealed record SetUserAccessRequest(string UserId, bool Suspended, string Reason, int ExpectedRevision);
