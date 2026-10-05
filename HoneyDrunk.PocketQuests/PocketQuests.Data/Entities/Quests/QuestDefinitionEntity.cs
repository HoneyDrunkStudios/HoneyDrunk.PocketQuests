namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one account-owned persistent quest identity, custom or adopted from the system catalog. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestDefinitionEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets system template identity; null means an editable custom definition.</summary>
    public string? SystemQuestId { get; set; }

    /// <summary>Gets or sets current content revision number; matching immutable revision is committed by the controlled writer.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets instant this definition was archived; existing occurrences retain history. UTC instant. Null means this event has not happened.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets sQL-generated opaque concurrency token; compare as bytes, not as a clock.</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.</summary>
    public int CreationOrdinal { get; set; }

    /// <summary>Gets or sets original accepted UUID spelling for API compatibility; the typed Id remains the ownership key. Null is reserved for public catalog adoption or the retained v1 adapter.</summary>
    public string? ClientKey { get; set; }
}
