namespace PocketQuests.Domain.Models.Accounts;

/// <summary>Identity service wire values consumed by Pocket Quests.</summary>
public static class IdentityProtocol
{
    /// <summary>The registered lifecycle consumer ID.</summary>
    public const string ConsumerId = "pocketquests";

    /// <summary>An account that permits normal product access.</summary>
    public const string Active = "Active";

    /// <summary>An account within its recovery period.</summary>
    public const string Inactive = "Inactive";

    /// <summary>An account whose irreversible erasure has begun.</summary>
    public const string Erasing = "Erasing";
}
