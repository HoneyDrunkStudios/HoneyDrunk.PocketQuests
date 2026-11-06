using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Schedules;

/// <summary>Durable series and pause history used for delivery, deadline shifts and streak replay.</summary>
/// <param name="Series">The recurring quest series included in this result.</param>
/// <param name="Pauses">The retained pause intervals.</param>
/// <param name="PausedCategories">The categories currently paused.</param>
/// <param name="AccountPaused">Whether the account is paused.</param>
public record ScheduleState(
    ImmutableArray<QuestSeries> Series,
    ImmutableArray<PauseWindow> Pauses,
    ImmutableArray<string> PausedCategories,
    bool AccountPaused = false)
{
    /// <summary>Gets an empty scheduling state without implicit recurrence.</summary>
    public static ScheduleState Empty { get; } = new([], [], []);
}
