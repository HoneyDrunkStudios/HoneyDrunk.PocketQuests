namespace PocketQuests.Data.Entities.Accounts;

/// <summary>One row is one current category selected as an account interest. Classification: Restricted. History: event; Current association only; remove when deselected and at final erasure. No historical interest tracking.</summary>
public sealed class AccountInterestEntity
{
    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets selected category; never enables recurrence implicitly.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets current selected category array position, zero-based and at most nine.</summary>
    public int Position { get; set; }
}
