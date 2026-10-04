namespace PocketQuests.Data.Entities.Lifecycle;

/// <summary>One row is one short-lived acknowledgment-envelope ownership binding for private Identity delivery. Classification: Restricted. History: plumbing; Delete after dispatch or within one hour. May outlive Account only long enough to complete the current private acknowledgment.</summary>
public sealed class LifecycleMessageEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets canonical external user whose lifecycle acknowledgment this is.</summary>
    public string IdentityUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets product account if still present; null before onboarding or after purge.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Gets or sets identity transition version being acknowledged.</summary>
    public long LifecycleVersion { get; set; }

    /// <summary>Gets or sets shared Data.Outbox row containing the acknowledgment capability and payload.</summary>
    public Guid OutboxMessageId { get; set; }

    /// <summary>Gets or sets hard delivery-envelope expiration; maximum one hour after creation, matching current protocol. UTC instant.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
