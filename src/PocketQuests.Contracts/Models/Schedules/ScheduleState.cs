using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public ScheduleState JSON contract, independent of storage and domain behavior.</summary>
public sealed record ScheduleState(ImmutableArray<QuestSeries> Series, ImmutableArray<PauseWindow> Pauses,
    ImmutableArray<string> PausedCategories, bool AccountPaused);
