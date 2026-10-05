namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public Balance JSON contract, independent of storage and domain behavior.</summary>
public sealed record Balance(string Id, string Name, long Xp, int Level);
