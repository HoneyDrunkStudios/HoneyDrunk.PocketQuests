using PocketQuests.Api.Contracts.Catalogs;
using PocketQuests.Api.Contracts.Commands;
using PocketQuests.Api.Contracts.Common;
using PocketQuests.Api.Contracts.Exports;
using PocketQuests.Api.Contracts.Profiles;
using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Projections;
using PocketQuests.Api.Contracts.Quests;
using PocketQuests.Api.Contracts.Schedules;
using PocketQuests.Api.Contracts.Synchronization;
using System.Collections.Immutable;

namespace PocketQuests.Api.Contracts.Projections;

/// <summary>Explicit mappings for the projections HTTP contracts.</summary>
public static class ProjectionsContractMapping
{
    /// <summary>Maps CompletionLevelUp explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Projections.CompletionLevelUp ToContract(this PocketQuests.Domain.Models.Quests.CompletionLevelUp value) => new(
        value.Track,
        value.TrackId,
        value.Name,
        value.From,
        value.To);

    /// <summary>Maps CompletionOutcome explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Projections.CompletionOutcome ToContract(this PocketQuests.Domain.Models.Quests.CompletionOutcome value) => new(
        value.CompletionId,
        value.OccurrenceId,
        value.LevelUps.Select(item => item.ToContract()).ToImmutableArray(),
        (PocketQuests.Api.Contracts.Progress.Rank?)value.RankUp,
        value.Unlocks.Select(item => item.ToContract()).ToImmutableArray());

    /// <summary>Maps OccurrenceView explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Projections.OccurrenceView ToContract(this PocketQuests.Domain.Models.Quests.OccurrenceView value) => new(
        value.Occurrence.ToContract(),
        (PocketQuests.Api.Contracts.Quests.Occurrences.QuestStatus)value.Status,
        value.Completion?.ToContract(),
        value.CanUndo,
        value.Planned?.ToContract());

    /// <summary>Maps QuestState explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Projections.QuestState ToContract(this PocketQuests.Domain.Models.Quests.QuestState value) => new(
        value.Zone,
        value.Today,
        value.Occurrences.Select(item => item.ToContract()).ToImmutableArray(),
        value.OverallXp,
        value.OverallLevel,
        value.Categories.Select(item => item.ToContract()).ToImmutableArray(),
        value.Attributes.Select(item => item.ToContract()).ToImmutableArray(),
        value.Skills.Select(item => item.ToContract()).ToImmutableArray(),
        value.Rank.ToContract(),
        value.Streaks.Select(item => item.ToContract()).ToImmutableArray(),
        value.Entitlements.Select(item => item.ToContract()).ToImmutableArray(),
        value.Definitions.Select(item => item.ToContract()).ToImmutableArray(),
        value.Profile.ToContract(),
        value.Schedule.ToContract(),
        value.Penalties.Select(item => item.ToContract()).ToImmutableArray(),
        value.Ledger.Select(item => item.ToContract()).ToImmutableArray(),
        value.FutureWarnings,
        value.CompletionOutcome?.ToContract());
}
