namespace PocketQuests.Data.Entities.Lifecycle;

/// <summary>One row is one latest accepted lifecycle fence for a canonical Identity user, possibly before onboarding. Classification: Restricted. History: mutable; Retain while required to fence the account/pre-onboarding identity. On verified erasure replace with minimal marker; no content history.</summary>
public sealed class AccountLifecycleStateEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets canonical external Identity user being fenced; never a provider identity.</summary>
    public string IdentityUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets local product profile if one exists; null before onboarding or after purge.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Gets or sets highest accepted monotonic Identity lifecycle version; old deliveries cannot reduce it.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets active, Inactive or Erasing, as defined by the reviewed Identity lifecycle contract.</summary>
    public string StateCode { get; set; } = string.Empty;

    /// <summary>Gets or sets authoritative Identity transition instant. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets original deletion-request pause origin, retained across cancellation. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? PausedAt { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];
}
