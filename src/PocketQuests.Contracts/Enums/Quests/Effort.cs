namespace PocketQuests.Contracts.Enums.Quests;

/// <summary>The calibrated base reward tier, independent of rank.</summary>
public enum Effort
{
    /// <summary>Ten base XP before the rank multiplier.</summary>
    Small,

    /// <summary>Eighty base XP before the rank multiplier.</summary>
    Medium,

    /// <summary>Eight hundred base XP before the rank multiplier.</summary>
    Large
}
