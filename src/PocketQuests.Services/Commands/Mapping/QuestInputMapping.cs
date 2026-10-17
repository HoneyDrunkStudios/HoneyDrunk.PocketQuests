using PocketQuests.Contracts.Requests.Commands;
using System.Collections.Immutable;

namespace PocketQuests.Services.Commands.Mapping;

/// <summary>Explicit input mappings that leave reward computation in the domain.</summary>
public static class QuestInputMapping
{
    /// <summary>Maps Quest explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Domain.Models.Quests.Quest ToModel(this QuestInput value) => new(
        value.Id,
        value.Title,
        value.Criterion,
        value.CategoryId,
        (PocketQuests.Domain.Models.Progress.Rank)value.Rank,
        (PocketQuests.Domain.Models.Quests.Effort)value.Effort,
        value.Attributes.Select(item => item.ToModel()).ToImmutableArray(),
        value.Skills.Select(item => item.ToModel()).ToImmutableArray(),
        value.IsCustom,
        value.Description,
        value.PenaltyPercent);

    /// <summary>Maps Share explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Domain.Models.Quests.Share ToModel(this ShareInput value) => new(
        value.Id,
        value.BasisPoints);

    /// <summary>Maps domain quest terms to the compatible command input shape.</summary>
    /// <param name="value">The source terms.</param>
    /// <returns>The input contract.</returns>
    public static QuestInput ToInput(this PocketQuests.Domain.Models.Quests.Quest value) => new(
        value.Id,
        value.Title,
        value.Criterion,
        value.CategoryId,
        (PocketQuests.Contracts.Enums.Progress.Rank)value.Rank,
        (PocketQuests.Contracts.Enums.Quests.Effort)value.Effort,
        value.Attributes.Select(item => item.ToInput()).ToImmutableArray(),
        value.Skills.Select(item => item.ToInput()).ToImmutableArray(),
        value.IsCustom,
        value.Description,
        value.PenaltyPercent,
        value.BaseXp);

    /// <summary>Maps a domain allocation to the input contract.</summary>
    /// <param name="value">The source allocation.</param>
    /// <returns>The input allocation.</returns>
    public static ShareInput ToInput(this PocketQuests.Domain.Models.Quests.Share value) => new(value.Id, value.BasisPoints);
}
