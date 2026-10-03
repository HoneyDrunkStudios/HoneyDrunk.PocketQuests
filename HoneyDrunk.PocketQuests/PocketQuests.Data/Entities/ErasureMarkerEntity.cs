namespace PocketQuests.Data.Entities;

/// <summary>Minimal non-content marker retained for 35 days after verified product erasure.</summary>
public sealed class ErasureMarkerEntity
{
    /// <summary>Gets or sets the opaque canonical account ID.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the verified live-erasure instant.</summary>
    public DateTimeOffset ErasedAt { get; set; }
}
