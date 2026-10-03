namespace PocketQuests.Data.Entities;

/// <summary>Maps a shared Identity user to one isolated quest account.</summary>
public sealed class AccountEntity
{
    /// <summary>Gets or sets the internal SQL account key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the unique digest of the trusted issuer and subject.</summary>
    public string IdentityKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the validated account IANA timezone.</summary>
    public string Zone { get; set; } = string.Empty;

    /// <summary>Gets or sets the server timestamp of initial setup.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the latest committed command time; null marks accounts created before clock ordering was persisted.</summary>
    public DateTimeOffset? LastRecordedAt { get; set; }

    /// <summary>Gets or sets preferences and replaceable skill seeds.</summary>
    public string? Profile { get; set; }

    /// <summary>Gets or sets durable schedule and effective pause history.</summary>
    public string? Schedule { get; set; }
}
