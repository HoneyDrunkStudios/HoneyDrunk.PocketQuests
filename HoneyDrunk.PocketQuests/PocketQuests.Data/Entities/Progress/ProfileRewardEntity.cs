namespace PocketQuests.Data.Entities.Progress;

/// <summary>One row is one public achievement, badge or frame definition. Classification: Public. History: reference; Retain referenced reward codes; history is the versioned catalog. Not erased with accounts.</summary>
public sealed class ProfileRewardEntity
{
    /// <summary>Gets or sets stable reward code, including existing A01/A02/B01/B02/F01/F02 IDs.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets achievement, Badge or Frame; only Badge and Frame are selectable.</summary>
    public string KindCode { get; set; } = string.Empty;

    /// <summary>Gets or sets public display name of this reward.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets nonnegative qualifying count in the versioned reward rule.</summary>
    public int RequiredCount { get; set; }

    /// <summary>Gets or sets minimum global rank in the versioned reward rule.</summary>
    public string RequiredRank { get; set; } = string.Empty;

    /// <summary>Gets or sets reviewed catalog/ruleset version that defines the qualifying predicate.</summary>
    public string RulesetVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }
}
