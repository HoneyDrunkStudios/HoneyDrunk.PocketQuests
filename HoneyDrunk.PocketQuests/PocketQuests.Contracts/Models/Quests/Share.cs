namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public Share JSON contract, independent of storage and domain behavior.</summary>
public sealed record Share(string Id, int BasisPoints);
