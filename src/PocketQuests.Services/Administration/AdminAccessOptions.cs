namespace PocketQuests.Services.Administration;

/// <summary>The single explicitly configured canonical operator; empty means no administrative access.</summary>
public sealed class AdminAccessOptions
{
    /// <summary>Gets or sets the trusted HoneyDrunk.Identity usr_ identifier. Never match by email or provider claims.</summary>
    public string UserId { get; set; } = string.Empty;
}
