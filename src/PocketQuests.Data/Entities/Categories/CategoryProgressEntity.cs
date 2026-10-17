namespace PocketQuests.Data.Entities.Categories;

/// <summary>One row is one current account/category streak and explicit pause setting. Classification: Restricted. History: projection; Current projection plus explicit category switch; source pause intervals are retained. Erase with account.</summary>
public sealed class CategoryProgressEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets category whose streak is displayed.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets nonnegative category streak length under pause/zone history.</summary>
    public int StreakDays { get; set; }

    /// <summary>Gets or sets integer category streak bonus percentage, from 0 through the approved maximum 20 under PQ-MVP-013.</summary>
    public int BonusRatePercent { get; set; }

    /// <summary>Gets or sets a value indicating whether the account qualified in this category on AsOfDate.</summary>
    public bool HasQualifiedToday { get; set; }

    /// <summary>Gets or sets a value indicating whether current explicit category-pause switch; distinct from the account-wide switch.</summary>
    public bool IsExplicitlyPaused { get; set; }

    /// <summary>Gets or sets account-calendar date used for the current-day flag.</summary>
    public DateOnly AsOfDate { get; set; }

    /// <summary>Gets or sets iANA timezone used for AsOfDate.</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>Gets or sets account projection generation of this summary.</summary>
    public long ProjectionVersion { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }
}
