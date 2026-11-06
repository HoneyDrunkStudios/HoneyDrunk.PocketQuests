namespace PocketQuests.Contracts.Responses.Discoveries;

/// <summary>Confirmation that a private saved place is absent.</summary>
/// <param name="Removed">Whether the resource was removed.</param>
public sealed record RemovalResult(bool Removed);
