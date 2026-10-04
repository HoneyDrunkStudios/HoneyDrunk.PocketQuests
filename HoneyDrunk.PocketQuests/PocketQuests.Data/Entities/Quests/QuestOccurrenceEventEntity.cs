namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one effective occurrence transition including completion, Undo or clock assessment. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestOccurrenceEventEntity
{
    /// <summary>Gets or sets stable effective event UUID, reused as the completion ID for Completed events.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets occurrence affected by this transition.</summary>
    public Guid QuestOccurrenceId { get; set; }

    /// <summary>Gets or sets immutable occurrence version relevant to the transition.</summary>
    public Guid QuestOccurrenceRevisionId { get; set; }

    /// <summary>Gets or sets accepted, Offered, Edited, Frozen, Resumed, Abandoned, Completed, Undone or DeadlineElapsed.</summary>
    public string EventCode { get; set; } = string.Empty;

    /// <summary>Gets or sets domain action/assessment time; never substituted with server receipt time. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets commit visibility order of this event.</summary>
    public long AccountMutationVersion { get; set; }

    /// <summary>Gets or sets receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.</summary>
    public Guid? CommandReceiptId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
