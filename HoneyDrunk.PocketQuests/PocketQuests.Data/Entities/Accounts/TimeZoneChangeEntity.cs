namespace PocketQuests.Data.Entities.Accounts;

/// <summary>One row is one recorded selection of a new personal calendar timezone. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class TimeZoneChangeEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets iANA zone before the change.</summary>
    public string FromTimeZoneId { get; set; } = string.Empty;

    /// <summary>Gets or sets iANA zone selected by the account.</summary>
    public string ToTimeZoneId { get; set; } = string.Empty;

    /// <summary>Gets or sets recorded time of the timezone change. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets receipt for the command that caused this fact.</summary>
    public Guid CommandReceiptId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
