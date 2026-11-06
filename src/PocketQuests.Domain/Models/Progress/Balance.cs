namespace PocketQuests.Domain.Models.Progress;

/// <summary>The current XP and derived level of one progression track.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Xp">The experience represented by this result.</param>
/// <param name="Level">The progression level.</param>
public record Balance(string Id, string Name, long Xp, int Level);
