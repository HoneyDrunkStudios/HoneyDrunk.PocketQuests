namespace PocketQuests.Data.Entities.Skills;

/// <summary>One row is one replacement experience placement effective at a recorded time. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class SkillAssessmentEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets system skill being referenced; null when CustomSkillId is supplied.</summary>
    public string? SystemSkillId { get; set; }

    /// <summary>Gets or sets custom skill owned by this account; null when SystemSkillId is supplied.</summary>
    public Guid? CustomSkillId { get; set; }

    /// <summary>Gets or sets new, Practiced, Experienced or Expert placement; this is not earned XP.</summary>
    public string ExperienceCode { get; set; } = string.Empty;

    /// <summary>Gets or sets exact placement component under RulesetVersion; replaces the prior seed without granting other pools.</summary>
    public long SeedXp { get; set; }

    /// <summary>Gets or sets rules version that maps experience placement to seed XP.</summary>
    public string RulesetVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets recorded placement time. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets receipt for the command that caused this fact.</summary>
    public Guid CommandReceiptId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
