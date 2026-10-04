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

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>Explicit mappings for the schedules HTTP contracts.</summary>
public static class SchedulesContractMapping
{
    /// <summary>Maps DeadlineChange explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Schedules.DeadlineChange ToContract(this PocketQuests.Domain.Models.Schedules.DeadlineChange value) => new(
        value.OccurrenceId,
        value.Title,
        value.Deadline,
        value.BecomesMissed);

    /// <summary>Maps PauseWindow explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Schedules.PauseWindow ToContract(this PocketQuests.Domain.Models.Schedules.PauseWindow value) => new(
        value.CategoryId,
        value.StartedAt,
        value.EndedAt);

    /// <summary>Maps PlannedMoment explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Schedules.PlannedMoment ToContract(this PocketQuests.Domain.Models.Schedules.PlannedMoment value) => new(
        value.Requested,
        value.Resolved,
        value.Instant,
        value.Adjusted,
        value.Repeated);

    /// <summary>Maps QuestSeries explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Schedules.QuestSeries ToContract(this PocketQuests.Domain.Models.Schedules.QuestSeries value) => new(
        value.Id,
        value.Quest.ToContract(),
        value.Anchor,
        (PocketQuests.Api.Contracts.Schedules.Cadence)value.Cadence,
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
    public static PocketQuests.Api.Contracts.Schedules.ScheduleState ToContract(this PocketQuests.Domain.Models.Schedules.ScheduleState value) => new(
        value.Series.Select(item => item.ToContract()).ToImmutableArray(),
        value.Pauses.Select(item => item.ToContract()).ToImmutableArray(),
        value.PausedCategories,
        value.AccountPaused);

    /// <summary>Maps ZoneChange explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Schedules.ZoneChange ToContract(this PocketQuests.Domain.Models.Schedules.ZoneChange value) => new(
        value.From,
        value.To,
        value.At);

    /// <summary>Maps ZonePreview explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Schedules.ZonePreview ToContract(this PocketQuests.Domain.Models.Schedules.ZonePreview value) => new(
        value.PreviousZone,
        value.Zone,
        value.Deadlines.Select(item => item.ToContract()).ToImmutableArray());
}
