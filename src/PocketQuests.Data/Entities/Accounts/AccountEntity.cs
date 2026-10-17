namespace PocketQuests.Data.Entities.Accounts;

/// <summary>One row is one personal Pocket Quests profile for a verified canonical Identity user. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class AccountEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets canonical never-recycled usr_ identifier returned by HoneyDrunk.Identity, not a provider subject or email.</summary>
    public string IdentityUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets selected IANA timezone used for personal calendar calculations, not a UTC offset.</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether explicit product onboarding completed.</summary>
    public bool IsOnboardingComplete { get; set; }

    /// <summary>Gets or sets a value indicating whether the account requested optional grouped expiry warnings; device OS permission is separate.</summary>
    public bool HasExpiryWarnings { get; set; }

    /// <summary>Gets or sets a value indicating whether current explicit all-category pause setting; effective intervals are stored separately.</summary>
    public bool IsAccountPaused { get; set; }

    /// <summary>Gets or sets selected badge code; null means no selected badge.</summary>
    public string? SelectedBadgeId { get; set; }

    /// <summary>Gets or sets constant Badge discriminator for the typed reward FK.</summary>
    public string SelectedBadgeKind { get; set; } = "Badge";

    /// <summary>Gets or sets selected frame code; null means no selected frame.</summary>
    public string? SelectedFrameId { get; set; }

    /// <summary>Gets or sets constant Frame discriminator for the typed reward FK.</summary>
    public string SelectedFrameKind { get; set; } = "Frame";

    /// <summary>Gets or sets fixed account logical-time high-water mark; never a future deadline. UTC instant.</summary>
    public DateTimeOffset LastRecordedAt { get; set; }

    /// <summary>Gets or sets monotonic committed account mutation number; issued anchors retain this visibility version.</summary>
    public long MutationVersion { get; set; }

    /// <summary>Gets or sets mutation version fully reflected by the current materialized progress projection.</summary>
    public long ProjectionVersion { get; set; }

    /// <summary>Gets or sets clock instant through which time-driven projection state is reconciled. UTC instant.</summary>
    public DateTimeOffset ProjectionAsOfAt { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether bounded recurrence delivery has more work at the latest requested clock; ProjectionAsOfAt advances only when that work is complete.</summary>
    public bool HasPendingReconciliation { get; set; }
}
