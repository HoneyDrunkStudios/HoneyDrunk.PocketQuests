namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one immutable recurrence configuration and consent state. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestSeriesRevisionEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets series to which this version belongs.</summary>
    public Guid QuestSeriesId { get; set; }

    /// <summary>Gets or sets positive configuration sequence within a series.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets frozen terms scheduled by this version.</summary>
    public Guid QuestDefinitionRevisionId { get; set; }

    /// <summary>Gets or sets calendar recurrence unit: Days, Weeks, Months or Years.</summary>
    public string CadenceCode { get; set; } = string.Empty;

    /// <summary>Gets or sets calendar-unit interval 1..999, matching current validation.</summary>
    public int Interval { get; set; }

    /// <summary>Gets or sets first delivery date in the selected account calendar; must precede year 9999.</summary>
    public DateOnly AnchorOn { get; set; }

    /// <summary>Gets or sets optional local planned clock time, not the completion deadline. Null means no planned time.</summary>
    public TimeOnly? PlannedTime { get; set; }

    /// <summary>Gets or sets a value indicating whether explicit consent to automatically accept this version of recurring penalty terms.</summary>
    public bool HasAutoAcceptPenalty { get; set; }

    /// <summary>Gets or sets when this configuration became effective. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets receipt for the command that caused this fact.</summary>
    public Guid CommandReceiptId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets public domain schedule version; internal revision also changes when definition terms change without editing recurrence.</summary>
    public int ScheduleVersion { get; set; }
}
