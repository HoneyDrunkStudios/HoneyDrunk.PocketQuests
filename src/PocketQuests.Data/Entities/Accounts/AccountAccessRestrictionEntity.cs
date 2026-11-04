namespace PocketQuests.Data.Entities.Accounts;

/// <summary>One row is the current Pocket Quests access restriction for one account. Classification: Restricted. History: mutable current row with actor stamps and rowversion; shared action audit excludes reasons and is erased with the account. Retain only while the product account exists; no temporal copies of reasons.</summary>
public sealed class AccountAccessRestrictionEntity
{
    /// <summary>Gets or sets application-generated stable identifier for the current account moderation record.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the Pocket Quests profile whose product access is restricted; not a global Identity suspension.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets a value indicating whether Pocket Quests use is denied until explicit reactivation.</summary>
    public bool IsSuspended { get; set; }

    /// <summary>Gets or sets current private operator reason for suspension or reactivation; no previous reason copies are retained.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets positive edit revision checked under the account command lock; stale changes must reload.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets canonical HoneyDrunk.Identity usr_ operator identifier that created this record; not a provider subject or email.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets canonical HoneyDrunk.Identity usr_ operator identifier for the most recent explicit change.</summary>
    public string ModifiedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC creation instant for this current moderation record.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC instant of the most recent explicit moderation change, never before creation.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets the SQL-generated concurrency token, not a timestamp.</summary>
    public byte[] RowVersion { get; set; } = [];
}
