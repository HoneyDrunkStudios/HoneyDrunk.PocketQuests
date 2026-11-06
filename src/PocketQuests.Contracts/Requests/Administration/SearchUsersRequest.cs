namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>Search text stays in the body rather than access-log URLs.</summary>
/// <param name="Query">The query text or query parameters.</param>
/// <param name="After">The pagination position after the last included item.</param>
public sealed record SearchUsersRequest(string? Query = null, string? After = null);
