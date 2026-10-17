namespace PocketQuests.Data.Entities.Accounts;

/// <summary>One row is one explicit account/category pause interval. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class AccountPauseEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets account or Category; distinguishes all-category intent from one-category intent.</summary>
    public string ScopeCode { get; set; } = string.Empty;

    /// <summary>Gets or sets paused category; null means account-wide scope.</summary>
    public string? CategoryId { get; set; }

    /// <summary>Gets or sets effective start of this pause. UTC instant.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets effective end of this pause. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Gets or sets receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.</summary>
    public Guid? CommandReceiptId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.</summary>
    public int CreationOrdinal { get; set; }
}
