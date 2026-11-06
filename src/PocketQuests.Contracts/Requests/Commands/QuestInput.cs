using PocketQuests.Contracts.Enums.Progress;
using PocketQuests.Contracts.Enums.Quests;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Requests.Commands;

/// <summary>Accepted quest input, retaining optional defaults and numeric-string binding.</summary>
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
/// <param name="BaseXp">The experience before reward modifiers.</param>
public sealed record QuestInput(
    string Id,
    string Title,
    string Criterion,
    string CategoryId,
    Rank Rank,
    Effort Effort,
    ImmutableArray<ShareInput> Attributes,
    ImmutableArray<ShareInput> Skills,
    bool IsCustom = false,
    string? Description = null,
    int PenaltyPercent = 0,
    int BaseXp = 0);
