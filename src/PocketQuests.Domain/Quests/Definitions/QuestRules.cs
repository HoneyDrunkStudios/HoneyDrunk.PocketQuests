namespace PocketQuests.Domain.Quests.Definitions;

/// <summary>Validation and timing rules shared by quest commands and projections.</summary>
public static class QuestRules
{
    /// <summary>Maximum custom quest title length.</summary>
    public const int MaximumTitleLength = 120;

    /// <summary>Maximum completion criterion length.</summary>
    public const int MaximumCriterionLength = 2_000;

    /// <summary>Maximum optional quest description length.</summary>
    public const int MaximumDescriptionLength = 2_000;

    /// <summary>Gets the elapsed-time window for reversing a completion.</summary>
    public static TimeSpan UndoWindow { get; } = TimeSpan.FromHours(24);
}
