namespace PocketQuests.Data.Entities.Progress;

/// <summary>One row is one current account/track-target projection with earned and seed components separated. Classification: Restricted. History: projection; Current projection only; rebuild from history and erase with account. No temporal copies.</summary>
public sealed class XpBalanceEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

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

    /// <summary>Gets or sets nonnegative earned balance after chronological zero-floor processing.</summary>
    public long EarnedXp { get; set; }

    /// <summary>Gets or sets nonnegative experience placement component; nonzero only for Skill.</summary>
    public long SeedXp { get; set; }

    /// <summary>Gets or sets positive level derived by the approved curve; zero XP starts at level 1.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets account projection generation to which this balance belongs.</summary>
    public long ProjectionVersion { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }
}
