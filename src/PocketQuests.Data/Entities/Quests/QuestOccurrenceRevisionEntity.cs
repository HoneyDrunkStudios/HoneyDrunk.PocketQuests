namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one immutable occurrence version visible at a committed account mutation. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestOccurrenceRevisionEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets occurrence whose terms and scheduling state this version captures.</summary>
    public Guid QuestOccurrenceId { get; set; }

    /// <summary>Gets or sets positive version within this occurrence.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets persistent account-owned definition identity.</summary>
    public Guid QuestDefinitionId { get; set; }

    /// <summary>Gets or sets exact immutable content/reward revision used by this occurrence version.</summary>
    public Guid QuestDefinitionRevisionId { get; set; }

    /// <summary>Gets or sets materialized current/frozen category for filtering, constrained to the controlled revision write.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets due date in DeadlineTimeZoneId; null means unscheduled with no deadline.</summary>
    public DateOnly? DueOn { get; set; }

    /// <summary>Gets or sets optional local planned clock time; null means none; requires DueOn.</summary>
    public TimeOnly? PlannedTime { get; set; }

    /// <summary>Gets or sets absolute end-of-due-day deadline; null for unscheduled occurrence. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? DeadlineAt { get; set; }

    /// <summary>Gets or sets iANA calendar zone used to calculate DueOn/DeadlineAt. Null only for unscheduled occurrence.</summary>
    public string? DeadlineTimeZoneId { get; set; }

    /// <summary>Gets or sets active, Completed, Missed, Frozen, Abandoned or Offered; time-dependent projection can be newer than persisted reconciliation.</summary>
    public string StateCode { get; set; } = string.Empty;

    /// <summary>Gets or sets recorded acceptance time; null while only an unaccepted offer. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? AcceptedAt { get; set; }

    /// <summary>Gets or sets start of the current freeze, if any. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? FrozenAt { get; set; }

    /// <summary>Gets or sets a value indicating whether an individual freeze remains after account/category resume.</summary>
    public bool IsIndividuallyFrozen { get; set; }

    /// <summary>Gets or sets explicit abandonment time, if any. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? AbandonedAt { get; set; }

    /// <summary>Gets or sets exact accepted nonnegative penalty amount. Null means no penalty commitment.</summary>
    public long? LockedLoss { get; set; }

    /// <summary>Gets or sets category frozen with an accepted loss; null exactly when LockedLoss is null.</summary>
    public string? LossCategoryId { get; set; }

    /// <summary>Gets or sets visibility order at commit; distinct from backdated EffectiveAt.</summary>
    public long AccountMutationVersion { get; set; }

    /// <summary>Gets or sets recorded effective time of the transition. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.</summary>
    public Guid? CommandReceiptId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets original occurrence creation/delivery instant used by the domain even while the recurring occurrence is still an unaccepted offer. Null means not applicable to this transition.</summary>
    public DateTimeOffset? OriginatedAt { get; set; }

    /// <summary>Gets or sets original domain/API display offset of OriginatedAt, retained separately from its authoritative UTC instant for exact recurring-offer response reconstruction.</summary>
    public short OriginatedOffsetMinutes { get; set; }
}
