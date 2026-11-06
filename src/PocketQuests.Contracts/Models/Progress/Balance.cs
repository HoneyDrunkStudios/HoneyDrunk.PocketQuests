namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public Balance JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Xp">The experience represented by this result.</param>
/// <param name="Level">The progression level.</param>
public sealed record Balance(string Id, string Name, long Xp, int Level);
