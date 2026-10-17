using PocketQuests.Services.Catalogs.Mapping;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Common.Mapping;
using PocketQuests.Services.Exports.Mapping;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Schedules.Mapping;
using PocketQuests.Services.Synchronization.Mapping;
using System.Collections.Immutable;

namespace PocketQuests.Services.Projections.Mapping;

/// <summary>Explicit mappings for the projections HTTP contracts.</summary>
public static class ProjectionsContractMapping
{
    /// <summary>Maps CompletionLevelUp explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Projections.CompletionLevelUp ToModel(this PocketQuests.Domain.Models.Quests.CompletionLevelUp value) => new(
        value.Track,
        value.TrackId,
        value.Name,
        value.From,
        value.To);

    /// <summary>Maps CompletionOutcome explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Projections.CompletionOutcome ToModel(this PocketQuests.Domain.Models.Quests.CompletionOutcome value) => new(
        value.CompletionId,
        value.OccurrenceId,
        value.LevelUps.Select(item => item.ToModel()).ToImmutableArray(),
        (PocketQuests.Contracts.Enums.Progress.Rank?)value.RankUp,
        value.Unlocks.Select(item => item.ToModel()).ToImmutableArray());

    /// <summary>Maps OccurrenceView explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Projections.OccurrenceView ToModel(this PocketQuests.Domain.Models.Quests.OccurrenceView value) => new(
        value.Occurrence.ToModel(),
        (PocketQuests.Contracts.Enums.Quests.QuestStatus)value.Status,
        value.Completion?.ToModel(),
        value.CanUndo,
        value.Planned?.ToModel());

    /// <summary>Maps QuestState explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Responses.Projections.QuestState ToModel(this PocketQuests.Domain.Models.Quests.QuestState value) => new(
        value.Zone,
        value.Today,
        value.Occurrences.Select(item => item.ToModel()).ToImmutableArray(),
        value.OverallXp,
        value.OverallLevel,
        value.Categories.Select(item => item.ToModel()).ToImmutableArray(),
        value.Attributes.Select(item => item.ToModel()).ToImmutableArray(),
        value.Skills.Select(item => item.ToModel()).ToImmutableArray(),
        value.Rank.ToModel(),
        value.Streaks.Select(item => item.ToModel()).ToImmutableArray(),
        value.Entitlements.Select(item => item.ToModel()).ToImmutableArray(),
        value.Definitions.Select(item => item.ToModel()).ToImmutableArray(),
        value.Profile.ToModel(),
        value.Schedule.ToModel(),
        value.Penalties.Select(item => item.ToModel()).ToImmutableArray(),
        value.Ledger.Select(item => item.ToModel()).ToImmutableArray(),
        value.FutureWarnings,
        value.CompletionOutcome?.ToModel());
}
