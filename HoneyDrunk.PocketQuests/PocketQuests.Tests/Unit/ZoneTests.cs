using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Tests.Unit;

/// <summary>Timezone transition, DST preview and notification eligibility acceptance coverage.</summary>
public sealed class ZoneTests
{
    /// <summary>Final history retains cutoffs while an active deadline moved into the past becomes missed.</summary>
    [Fact]
    public void ZoneChangePreservesFinalHistoryAndRejudgesOnlyUnfinishedActiveDeadlines()
    {
        var at = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var aggregate = new QuestAggregate("UTC");
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-27"), at.AddDays(-1));
        var missed = aggregate.Occurrences.Last();
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28"), at);
        var completed = aggregate.Occurrences.Last();
        aggregate.Apply(new(Guid.NewGuid(), "complete", completed.Id), at);
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28"), at);
        var active = aggregate.Occurrences.Last();
        aggregate.Apply(new(Guid.NewGuid(), "zone", NewZone: "Pacific/Kiritimati", ExpectedZone: "UTC", ConfirmZoneChange: true), at);
        var state = aggregate.Project(at);
        Assert.Equal(missed.Deadline, state.Occurrences.Single(o => o.Occurrence.Id == missed.Id).Occurrence.Deadline);
        Assert.Equal(completed.Deadline, state.Occurrences.Single(o => o.Occurrence.Id == completed.Id).Occurrence.Deadline);
        Assert.Equal(QuestStatus.Completed, state.Occurrences.Single(o => o.Occurrence.Id == completed.Id).Status);
        Assert.Equal(QuestStatus.Missed, state.Occurrences.Single(o => o.Occurrence.Id == active.Id).Status);
        Assert.Equal(10, state.OverallXp);
    }

    /// <summary>The zone jump during a freeze adds zero days; only actual local midnights in each segment count.</summary>
    [Fact]
    public void FreezeSegmentsExcludeDateLineJumpAndPreserveRecurrenceAnchor()
    {
        var at = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var aggregate = new QuestAggregate("UTC");
        aggregate.Apply(new(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Months, Interval: 1, ExpectedRevision: 0), at);
        aggregate.Apply(new(Guid.NewGuid(), "pause"), at);
        aggregate.Apply(new(Guid.NewGuid(), "zone", NewZone: "Pacific/Kiritimati", ExpectedZone: "UTC", ConfirmZoneChange: true), at.AddHours(1));
        Assert.Equal(QuestStatus.Frozen, aggregate.Project(at.AddHours(1)).Occurrences.Single().Status);
        aggregate.Apply(new(Guid.NewGuid(), "resume"), at.AddHours(23));
        Assert.Equal("2026-09-02", aggregate.Occurrences.Single().DueDate);
        Assert.Equal(1, aggregate.Schedule.Series.Single().PauseDays);
        Assert.Equal("2026-09-01", aggregate.Schedule.Series.Single().Anchor);
    }

    /// <summary>Repeated local dates use their credited rate without adding another streak day.</summary>
    [Fact]
    public void DateLineTransitionsDoNotMintStreakDays()
    {
        var at = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var aggregate = new QuestAggregate("UTC");
        Complete(at);
        aggregate.Apply(new(Guid.NewGuid(), "zone", NewZone: "Pacific/Kiritimati", ExpectedZone: "UTC", ConfirmZoneChange: true), at.AddHours(1));
        Complete(at.AddHours(1));
        Assert.Equal(1, aggregate.Project(at.AddHours(1)).Streaks.Single(s => s.CategoryId == "c07").Days);
        Complete(at.AddHours(23));
        Assert.Equal(2, aggregate.Project(at.AddHours(23)).Streaks.Single(s => s.CategoryId == "c07").Days);
        aggregate.Apply(new(Guid.NewGuid(), "zone", NewZone: "UTC", ExpectedZone: "Pacific/Kiritimati", ConfirmZoneChange: true), at.AddHours(24));
        Complete(at.AddHours(24));
        Assert.Equal(1, aggregate.Project(at.AddHours(24)).Streaks.Single(s => s.CategoryId == "c07").Days);
        Complete(at.AddHours(48));
        Assert.Equal(2, aggregate.Project(at.AddHours(48)).Streaks.Single(s => s.CategoryId == "c07").Days);
        Complete(at.AddHours(72));
        Assert.Equal(3, aggregate.Project(at.AddHours(72)).Streaks.Single(s => s.CategoryId == "c07").Days);

        void Complete(DateTimeOffset when)
        {
            aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), when);
            aggregate.Apply(new(Guid.NewGuid(), "complete", aggregate.Occurrences.Last().Id), when);
        }
    }

    /// <summary>A forward date jump preserves an already established streak without minting another day.</summary>
    [Fact]
    public void ForwardZoneJumpDoesNotResetEstablishedStreak()
    {
        var at = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var aggregate = new QuestAggregate("UTC");
        for (var day = 0; day < 3; day++)
        {
            aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), at.AddDays(day));
            aggregate.Apply(new(Guid.NewGuid(), "complete", aggregate.Occurrences.Last().Id), at.AddDays(day));
        }

        var changed = at.AddDays(2).AddHours(1);
        aggregate.Apply(new(Guid.NewGuid(), "zone", NewZone: "Pacific/Kiritimati", ExpectedZone: "UTC", ConfirmZoneChange: true), changed);
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), changed);
        aggregate.Apply(new(Guid.NewGuid(), "complete", aggregate.Occurrences.Last().Id), changed);
        Assert.Equal(3, aggregate.Project(changed).Streaks.Single(s => s.CategoryId == "c07").Days);
    }

    /// <summary>Clock previews show next valid gap time and first repeated occurrence with its offset.</summary>
    [Fact]
    public void PlannedClockGapsAndOverlapsAreDisclosed()
    {
        var gap = Scheduling.Planned("2026-03-08", "02:30", "America/New_York");
        Assert.True(gap.Adjusted);
        Assert.Equal("2026-03-08 03:00", gap.Resolved);
        var repeated = Scheduling.Planned("2026-11-01", "01:30", "America/New_York");
        Assert.True(repeated.Repeated);
        Assert.Equal(TimeSpan.FromHours(-4), repeated.Instant.Offset);
    }

    /// <summary>Warning forecasts require explicit preference, omit offers, and disappear on pause.</summary>
    [Fact]
    public void WarningForecastFollowsConsentAndPause()
    {
        var at = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var aggregate = new QuestAggregate("UTC");
        aggregate.Apply(new(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0), at);
        Assert.Empty(aggregate.Project(at).FutureWarnings!);
        aggregate.Apply(new(Guid.NewGuid(), "expiry-warnings", ExpiryWarnings: true), at);
        Assert.Equal(60, aggregate.Project(at).FutureWarnings!.Count);
        aggregate.Apply(new(Guid.NewGuid(), "pause"), at);
        Assert.Empty(aggregate.Project(at).FutureWarnings!);
    }
}
