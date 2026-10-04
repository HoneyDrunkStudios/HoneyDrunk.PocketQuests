namespace PocketQuests.Data.Entities.Progress;

/// <summary>One row is one currently effective derived contribution from an authoritative occurrence event. Classification: Restricted. History: projection; Retain only the current derived ledger; replace affected suffix transactionally. Source events remain authoritative. Erase with account.</summary>
public sealed class XpLedgerEntryEntity
{
    /// <summary>Gets or sets stable derived-entry UUID from source event, contribution kind and target identity.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets source event whose contribution is represented.</summary>
    public Guid QuestOccurrenceEventId { get; set; }

    /// <summary>Gets or sets base, StreakBonus or Penalty; placement seeds are separate.</summary>
    public string ContributionCode { get; set; } = string.Empty;

    /// <summary>Gets or sets progression pool: Overall, Category, Attribute or Skill.</summary>
    public string TrackCode { get; set; } = string.Empty;

    /// <summary>Gets or sets category target; null unless TrackCode is Category.</summary>
    public string? CategoryId { get; set; }

    /// <summary>Gets or sets attribute target; null unless TrackCode is Attribute.</summary>
    public string? AttributeId { get; set; }

    /// <summary>Gets or sets system skill target; null for other pools or a custom skill.</summary>
    public string? SystemSkillId { get; set; }

    /// <summary>Gets or sets account-owned custom skill target; null for other pools or a system skill.</summary>
    public Guid? CustomSkillId { get; set; }

    /// <summary>Gets or sets chronological application instant of the contribution. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets signed exact XP change; negative values are permitted for penalties only.</summary>
    public long Amount { get; set; }

    /// <summary>Gets or sets account projection generation represented by this current derived row.</summary>
    public long ProjectionVersion { get; set; }

    /// <summary>Gets or sets ruleset used when deriving this contribution.</summary>
    public string RulesetVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }
}
