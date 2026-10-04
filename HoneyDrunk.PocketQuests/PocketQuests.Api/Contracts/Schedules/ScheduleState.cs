using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>The public ScheduleState JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record ScheduleState(ImmutableArray<QuestSeries> Series, ImmutableArray<PauseWindow> Pauses,
    ImmutableArray<string> PausedCategories, bool AccountPaused);
