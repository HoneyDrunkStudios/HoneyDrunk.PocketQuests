namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one immutable successful completion with at most one write-once Undo annotation. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestCompletionEntity
{
    /// <summary>Gets or sets uUID of its Completed occurrence event and stable completion identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets occurrence completed by this fact.</summary>
    public Guid QuestOccurrenceId { get; set; }

    /// <summary>Gets or sets exact completed terms; never replaced by a later definition edit.</summary>
    public Guid QuestOccurrenceRevisionId { get; set; }

    /// <summary>Gets or sets trusted recorded completion time, including validated offline time. UTC instant.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>Gets or sets trusted recorded Undo time, if the completion was reversed. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? UndoneAt { get; set; }

    /// <summary>Gets or sets stable Undone event; null exactly when UndoneAt is null.</summary>
    public Guid? UndoQuestOccurrenceEventId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];
}
