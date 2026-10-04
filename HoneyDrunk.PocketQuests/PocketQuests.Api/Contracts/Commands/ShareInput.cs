namespace PocketQuests.Api.Contracts.Commands;

/// <summary>An input allocation that retains the existing numeric-string binding.</summary>
public sealed record ShareInput(string Id, int BasisPoints);
