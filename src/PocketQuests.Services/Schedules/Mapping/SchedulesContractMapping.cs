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

namespace PocketQuests.Services.Schedules.Mapping;

/// <summary>Explicit mappings for the schedules HTTP contracts.</summary>
public static class SchedulesContractMapping
{
    /// <summary>Maps DeadlineChange explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Schedules.DeadlineChange ToModel(this PocketQuests.Domain.Models.Schedules.DeadlineChange value) => new(
        value.OccurrenceId,
        value.Title,
        value.Deadline,
        value.BecomesMissed);

    /// <summary>Maps PauseWindow explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Schedules.PauseWindow ToModel(this PocketQuests.Domain.Models.Schedules.PauseWindow value) => new(
        value.CategoryId,
        value.StartedAt,
        value.EndedAt);

    /// <summary>Maps PlannedMoment explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Schedules.PlannedMoment ToModel(this PocketQuests.Domain.Models.Schedules.PlannedMoment value) => new(
        value.Requested,
        value.Resolved,
        value.Instant,
        value.Adjusted,
        value.Repeated);

    /// <summary>Maps QuestSeries explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Schedules.QuestSeries ToModel(this PocketQuests.Domain.Models.Schedules.QuestSeries value) => new(
        value.Id,
        value.Quest.ToModel(),
        value.Anchor,
        (PocketQuests.Contracts.Enums.Schedules.Cadence)value.Cadence,
        value.Interval,
        value.Version,
        value.NextSequence,
        value.PauseDays,
        value.Stopped,
        value.PlannedTime,
        value.AutoAcceptPenalty,
        value.EffectiveAt);

    /// <summary>Maps ScheduleState explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Schedules.ScheduleState ToModel(this PocketQuests.Domain.Models.Schedules.ScheduleState value) => new(
        value.Series.Select(item => item.ToModel()).ToImmutableArray(),
        value.Pauses.Select(item => item.ToModel()).ToImmutableArray(),
        value.PausedCategories,
        value.AccountPaused);

    /// <summary>Maps ZoneChange explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Schedules.ZoneChange ToModel(this PocketQuests.Domain.Models.Schedules.ZoneChange value) => new(
        value.From,
        value.To,
        value.At);

    /// <summary>Maps ZonePreview explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Schedules.ZonePreview ToModel(this PocketQuests.Domain.Models.Schedules.ZonePreview value) => new(
        value.PreviousZone,
        value.Zone,
        value.Deadlines.Select(item => item.ToModel()).ToImmutableArray());
}
