namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one opted-in recurrence series and its durable delivery cursor. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestSeriesEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets quest identity delivered by this recurrence series.</summary>
    public Guid QuestDefinitionId { get; set; }

    /// <summary>Gets or sets current immutable series configuration revision.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets next nonnegative sequence within the current series revision.</summary>
    public int NextSequence { get; set; }

    /// <summary>Gets or sets nonnegative local-calendar days shifted by effective pauses.</summary>
    public int PauseDays { get; set; }

    /// <summary>Gets or sets next delivery date in the selected account calendar; null when stopped or no representable next date.</summary>
    public DateOnly? NextDeliveryOn { get; set; }

    /// <summary>Gets or sets time recurrence was explicitly stopped; stopped series are never silently restarted. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? StoppedAt { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.</summary>
    public int CreationOrdinal { get; set; }
}
