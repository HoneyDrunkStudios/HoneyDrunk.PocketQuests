using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Progress;

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

    /// <summary>Validates reusable quest terms without account eligibility or persistence.</summary>
    /// <param name="input">The proposed quest terms.</param>
    /// <param name="skills">Skill identifiers permitted in this editing context.</param>
    public static void ValidateTerms(Quest input, IReadOnlySet<string> skills)
    {
        Progression.Require(Enum.IsDefined(input.Rank) && Enum.IsDefined(input.Effort), "Choose a supported rank and effort.");
        Progression.Require(input.Title?.Trim().Length is >= 1 and <= MaximumTitleLength, "Title: enter 1–120 characters.");
        Progression.Require(input.Criterion?.Trim().Length is >= 1 and <= MaximumCriterionLength, "Completion criterion: enter 1–2,000 characters describing the achieved outcome.");
        Progression.Require(input.Description is null || input.Description.Length <= MaximumDescriptionLength, "Description: use at most 2,000 characters.");
        Progression.Require(Catalog.Categories.Any(c => c.Id == input.CategoryId), "Category: choose one of the ten categories.");
        Progression.Require(!input.Attributes.IsDefault && !input.Skills.IsDefault, "Allocations: supply both pools, even when empty.");
        Progression.Require(input.Attributes.All(a => a is not null && Catalog.Attributes.Any(c => c.Id == a.Id)), "Attributes: choose known attributes.");
        Progression.Require(input.Skills.All(a => a is not null && skills.Contains(a.Id)), "Skills: choose known skills.");
        Progression.Require(input.PenaltyPercent is 0 or 10 or 25 or 50, "Penalty: choose none, 10%, 25% or 50%.");
        _ = Progression.Allocate(input.BaseXp, input.Attributes);
        _ = Progression.Allocate(input.BaseXp, input.Skills);
    }
}
