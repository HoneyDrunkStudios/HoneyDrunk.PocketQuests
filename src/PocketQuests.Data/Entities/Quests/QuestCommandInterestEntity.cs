namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one ordered category selected by a successful interests command, retained only to reconstruct its historical response. Classification: Restricted. History: immutable. Erase with its account; no age cutoff on replay history.</summary>
public sealed class QuestCommandInterestEntity
{
    /// <summary>Gets or sets owning account, shared with the parent history row.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets immutable interests transition owning this selected category.</summary>
    public Guid QuestCommandHistoryId { get; set; }

    /// <summary>Gets or sets selected public category code.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets original distinct selection order, matching the existing profile array.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets uTC server insertion instant.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
