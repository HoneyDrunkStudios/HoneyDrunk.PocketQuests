namespace PocketQuests.Data.Entities.Synchronization;

/// <summary>One row is one durable trusted clock/visibility proof for an account, device and process. Classification: Restricted. History: mutable; Retain compact proof while valid pending actions can reference it; no wall-clock expiry. Invalidate under lifecycle rules and erase with account. Safe retirement protocol is not implemented here.</summary>
public sealed class SyncAnchorEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets client device identifier, bound to the verified account; not an authentication credential.</summary>
    public Guid DeviceId { get; set; }

    /// <summary>Gets or sets process-session identity used for monotonic clock proof; no silent rewrite across restarts.</summary>
    public Guid BootId { get; set; }

    /// <summary>Gets or sets trusted server clock at proof issuance. UTC instant.</summary>
    public DateTimeOffset ServerAt { get; set; }

    /// <summary>Gets or sets device wall clock captured at issuance. UTC instant.</summary>
    public DateTimeOffset DeviceAt { get; set; }

    /// <summary>Gets or sets fixed issuance floor for already observed account history; never accumulated on refresh. UTC instant.</summary>
    public DateTimeOffset RecordedTimeFloorAt { get; set; }

    /// <summary>Gets or sets account version whose visible immutable occurrence revisions this proof authenticates.</summary>
    public long IssuedMutationVersion { get; set; }

    /// <summary>Gets or sets largest committed ordinal under this proof.</summary>
    public long LastOrdinal { get; set; }

    /// <summary>Gets or sets largest committed finite elapsed milliseconds under this proof. SQL float(53) preserves the existing API v1 IEEE-754 double, including fractional milliseconds; never rounded to an integer. Not money, XP, a wall clock or a duration added to the fixed issuance floor.</summary>
    public double LastElapsedMilliseconds { get; set; }

    /// <summary>Gets or sets lifecycle revocation time; null means not explicitly revoked. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? InvalidatedAt { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];
}
