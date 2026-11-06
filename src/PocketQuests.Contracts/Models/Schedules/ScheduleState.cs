using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public ScheduleState JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Series">The recurring quest series included in this result.</param>
/// <param name="Pauses">The retained pause intervals.</param>
/// <param name="PausedCategories">The categories currently paused.</param>
/// <param name="AccountPaused">Whether the account is paused.</param>
public sealed record ScheduleState(
    ImmutableArray<QuestSeries> Series,
    ImmutableArray<PauseWindow> Pauses,
    ImmutableArray<string> PausedCategories,
    bool AccountPaused);
