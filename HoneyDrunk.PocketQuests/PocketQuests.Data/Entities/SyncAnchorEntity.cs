namespace PocketQuests.Data.Entities;

/// <summary>Trusted clock baseline and snapshot for deterministic offline reconciliation.</summary>
public sealed class SyncAnchorEntity
{
    /// <summary>Gets or sets the owning account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the opaque anchor ID.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the installation identifier.</summary>
    public Guid DeviceId { get; set; }

    /// <summary>Gets or sets the process identifier whose monotonic clock was anchored.</summary>
    public Guid BootId { get; set; }

    /// <summary>Gets or sets server UTC at issuance.</summary>
    public DateTimeOffset ServerUtc { get; set; }

    /// <summary>Gets or sets device wall-clock baseline.</summary>
    public DateTimeOffset DeviceUtc { get; set; }

    /// <summary>Gets or sets the highest accepted device order.</summary>
    public long LastOrdinal { get; set; }

    /// <summary>Gets or sets the highest accepted monotonic duration.</summary>
    public double LastElapsedMilliseconds { get; set; }

    /// <summary>Gets or sets the immutable occurrence snapshot visible at issuance.</summary>
    public string Snapshot { get; set; } = string.Empty;
}
