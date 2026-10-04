namespace PocketQuests.Data.Entities.Skills;

/// <summary>One row is one account-owned custom skill, including archived skills. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class CustomSkillEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets trimmed custom skill display name, 1 to 80 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets versioned OrdinalIgnoreCase-equivalent name key; binary comparison. Archived names remain reserved.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>Gets or sets positive version of the shared product name-normalization algorithm.</summary>
    public short NameNormalizationVersion { get; set; }

    /// <summary>Gets or sets positive optimistic business revision, distinct from SQL RowVersion.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets instant the skill was archived; not a deletion marker. UTC instant. Null means this event has not happened.</summary>
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
