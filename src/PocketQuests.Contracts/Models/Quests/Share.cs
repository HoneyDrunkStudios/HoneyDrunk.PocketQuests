namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public Share JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="BasisPoints">The rate expressed in basis points.</param>
public sealed record Share(string Id, int BasisPoints);
