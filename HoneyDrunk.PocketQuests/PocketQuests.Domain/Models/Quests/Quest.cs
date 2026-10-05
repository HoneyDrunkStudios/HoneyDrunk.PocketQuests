using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Progress;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>Immutable reward terms for a catalog or custom quest.</summary>
public record Quest(string Id, string Title, string Criterion, string CategoryId, Rank Rank,
    Effort Effort, ImmutableArray<Share> Attributes, ImmutableArray<Share> Skills, bool IsCustom = false, string? Description = null, int PenaltyPercent = 0)
{
    /// <summary>Gets the calibrated base reward before category-only streak bonuses.</summary>
    public int BaseXp => Progression.Reward(Rank, Effort);
}
