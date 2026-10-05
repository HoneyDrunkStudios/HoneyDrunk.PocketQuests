namespace PocketQuests.Data.Entities.Synchronization;

/// <summary>One row is one immutable original outcome of a committed account command. Classification: Restricted. History: plumbing; Retain compact outcome and deduplication digest while the account exists. Delete at final account erasure; no replay TTL.</summary>
public sealed class CommandReceiptEntity
{
    /// <summary>Gets or sets stable client operation UUID; unchanged across retries.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets typed command discriminator under the stored API contract version.</summary>
    public string CommandType { get; set; } = string.Empty;

    /// <summary>Gets or sets positive version of the accepted wire command contract.</summary>
    public short ApiVersion { get; set; }

    /// <summary>Gets or sets sHA-256 of the versioned canonical command representation, excluding authentication secrets.</summary>
    public byte[] PayloadDigest { get; set; } = [];

    /// <summary>Gets or sets positive canonicalization/digest schema version; never hash incidental serializer output.</summary>
    public short DigestVersion { get; set; }

    /// <summary>Gets or sets positive schema version of the compact original command outcome.</summary>
    public short OutcomeVersion { get; set; }

    /// <summary>Gets or sets immutable compact original result and feedback, never the whole account or raw provider credentials. Limited to 65536 UTF-16 bytes.</summary>
    public string OutcomeJson { get; set; } = string.Empty;

    /// <summary>Gets or sets committed account mutation represented by this outcome; unchanged on retry.</summary>
    public long AppliedMutationVersion { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
