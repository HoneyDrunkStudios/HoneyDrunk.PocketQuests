using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Progress;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>Immutable reward terms for a catalog or custom quest.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Title">The human-readable title.</param>
/// <param name="Criterion">The criterion evaluated by the requirement.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="Rank">The quest or progression rank.</param>
/// <param name="Effort">The selected quest effort.</param>
/// <param name="Attributes">The character attributes in this snapshot.</param>
/// <param name="Skills">The skills included in this snapshot.</param>
/// <param name="IsCustom">Whether this resource is account-defined.</param>
/// <param name="Description">The human-readable description.</param>
/// <param name="PenaltyPercent">The penalty percentage in the quest terms.</param>
public record Quest(
    string Id,
    string Title,
    string Criterion,
    string CategoryId,
    Rank Rank,
    Effort Effort,
    ImmutableArray<Share> Attributes,
    ImmutableArray<Share> Skills,
    bool IsCustom = false,
    string? Description = null,
    int PenaltyPercent = 0)
{
    /// <summary>Gets the calibrated base reward before category-only streak bonuses.</summary>
    public int BaseXp => Progression.Reward(Rank, Effort);
}
