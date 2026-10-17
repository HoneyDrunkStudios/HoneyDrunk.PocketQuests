namespace PocketQuests.Domain.Models.Progress;

/// <summary>The current XP and derived level of one progression track.</summary>
public record Balance(string Id, string Name, long Xp, int Level);
