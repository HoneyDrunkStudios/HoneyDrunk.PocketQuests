namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>An explicit product suspension or reactivation with stale-edit protection and a private reason.</summary>
public sealed record SetUserAccessRequest(string UserId, bool Suspended, string Reason, int ExpectedRevision);
