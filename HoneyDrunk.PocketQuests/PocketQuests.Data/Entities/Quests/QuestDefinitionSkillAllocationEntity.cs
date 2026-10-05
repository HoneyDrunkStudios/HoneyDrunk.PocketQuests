namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one system/custom skill recipient in an immutable definition revision. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestDefinitionSkillAllocationEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets immutable revision that owns this allocation.</summary>
    public Guid QuestDefinitionRevisionId { get; set; }

    /// <summary>Gets or sets system skill being referenced; null when CustomSkillId is supplied.</summary>
    public string? SystemSkillId { get; set; }

    /// <summary>Gets or sets custom skill owned by this account; null when SystemSkillId is supplied.</summary>
    public Guid? CustomSkillId { get; set; }

    /// <summary>Gets or sets exact share 0..10000; nonempty allocation pool must sum to 10000 in the controlled write.</summary>
    public int BasisPoints { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets original allocation array position, preserving the existing API order; v1 rows retain their display-order adapter.</summary>
    public int Position { get; set; }
}
