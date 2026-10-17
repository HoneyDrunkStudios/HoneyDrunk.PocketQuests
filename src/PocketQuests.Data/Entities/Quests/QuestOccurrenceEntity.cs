namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one current offered or accepted occurrence, with immutable past versions below. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestOccurrenceEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

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

    /// <summary>Gets or sets positive immutable occurrence-version number matching this current row.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets recurrence source; null for a one-off quest.</summary>
    public Guid? QuestSeriesId { get; set; }

    /// <summary>Gets or sets exact recurrence configuration; null for a one-off quest.</summary>
    public Guid? QuestSeriesRevisionId { get; set; }

    /// <summary>Gets or sets nonnegative sequence within the source series version; null for a one-off quest.</summary>
    public int? SeriesSequence { get; set; }

    /// <summary>Gets or sets optional independently attested Large parent owned by the same account; null means no parent.</summary>
    public Guid? ParentQuestOccurrenceId { get; set; }

    /// <summary>Gets or sets proof under which an occurrence was created offline; null for ordinary online creation.</summary>
    public Guid? SourceSyncAnchorId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets original occurrence creation/delivery instant used by the domain even while the recurring occurrence is still an unaccepted offer. Null means not applicable to this transition.</summary>
    public DateTimeOffset? OriginatedAt { get; set; }

    /// <summary>Gets or sets stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.</summary>
    public int CreationOrdinal { get; set; }

    /// <summary>Gets or sets original domain/API display offset of OriginatedAt, retained separately from its authoritative UTC instant for exact recurring-offer response reconstruction.</summary>
    public short OriginatedOffsetMinutes { get; set; }
}
