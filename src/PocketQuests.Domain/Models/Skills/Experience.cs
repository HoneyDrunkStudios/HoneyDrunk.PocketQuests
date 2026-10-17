namespace PocketQuests.Domain.Models.Skills;

/// <summary>Onboarding experience tiers used only to seed skills.</summary>
public enum Experience
{
    /// <summary>No seeded XP.</summary>
    New,

    /// <summary>Seeds 405 skill XP.</summary>
    Practiced,

    /// <summary>Seeds 5780 skill XP.</summary>
    Experienced,

    /// <summary>Seeds 12005 skill XP.</summary>
    Expert
}
