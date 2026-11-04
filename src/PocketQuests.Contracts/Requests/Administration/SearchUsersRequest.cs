namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>Search text stays in the body rather than access-log URLs.</summary>
public sealed record SearchUsersRequest(string? Query = null, string? After = null);
