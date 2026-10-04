namespace PocketQuests.Api.Contracts.Progress;

/// <summary>Progression curves with separate calibrated coefficients.</summary>
public enum Track
{
    /// <summary>Base reward history with coefficient 100.</summary>
    Overall,

    /// <summary>Life-category progress with coefficient 10.</summary>
    Category,

    /// <summary>Allocated attribute progress with coefficient 25.</summary>
    Attribute,

    /// <summary>Allocated and seeded skill progress with coefficient 5.</summary>
    Skill
}
