namespace PocketQuests.Contracts.Requests.Commands;

/// <summary>An input allocation that retains the existing numeric-string binding.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="BasisPoints">The rate expressed in basis points.</param>
public sealed record ShareInput(string Id, int BasisPoints);
