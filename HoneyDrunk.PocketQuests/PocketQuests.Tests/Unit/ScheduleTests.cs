using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Tests.Unit;

/// <summary>Acceptance tests for original-anchor recurrence, effective freeze unions and locked losses.</summary>
public sealed class ScheduleTests
{
    /// <summary>Verifies January anchors survive pause offsets and duplicate reconciliation.</summary>
    [Fact]
    public void MonthlyAnchorAndOverlappingPausesShiftExactlyOnce()
    {
        var now = At(2027, 1, 31);
        var aggregate = new QuestAggregate("America/New_York");
        var seriesId = Guid.NewGuid();
        aggregate.Apply(new(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2027-01-31", SeriesId: seriesId, Cadence: Cadence.Months, Interval: 1, ExpectedRevision: 0), now);
        aggregate.Apply(new(Guid.NewGuid(), "pause", CategoryId: "c07"), now);
        aggregate.Apply(new(Guid.NewGuid(), "pause"), now.AddHours(1));
        aggregate.Apply(new(Guid.NewGuid(), "resume", CategoryId: "c07"), now.AddHours(2));
        Assert.Equal(QuestStatus.Frozen, aggregate.Project(now.AddDays(1)).Occurrences.Single().Status);
        aggregate.Apply(new(Guid.NewGuid(), "resume"), now.AddDays(1));
        Assert.Equal("2027-02-01", aggregate.Occurrences.Single().DueDate);
        aggregate.Reconcile(At(2027, 3, 1));
        aggregate.Reconcile(At(2027, 3, 1));
        Assert.Equal(2, aggregate.Occurrences.Count);
        Assert.Equal("2027-03-01", aggregate.Occurrences.Last().DueDate);
        aggregate.Reconcile(At(2027, 4, 1));
        Assert.Equal("2027-04-01", aggregate.Occurrences.Last().DueDate);
        Assert.Equal(1, aggregate.Schedule.Series.Single().PauseDays);
    }

    /// <summary>Verifies permanent stop freezes commitments without restarting on individual resume.</summary>
    [Fact]
    public void StopAndResumeOccurrenceDoNotRestartSeries()
    {
        var now = At(2026, 9, 28);
        var aggregate = new QuestAggregate("UTC");
        var seriesId = Guid.NewGuid();
        aggregate.Apply(new(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28", SeriesId: seriesId, Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0), now);
        var occurrence = aggregate.Occurrences.Single();
        aggregate.Apply(new(Guid.NewGuid(), "stop-series", SeriesId: seriesId), now);
        aggregate.Reconcile(now.AddDays(20));
        Assert.Single(aggregate.Occurrences);
        aggregate.Apply(new(Guid.NewGuid(), "resume-occurrence", occurrence.Id), now.AddDays(20));
        Assert.Equal("2026-10-18", aggregate.Occurrences.Single().DueDate);
        aggregate.Apply(new(Guid.NewGuid(), "complete", occurrence.Id), now.AddDays(20));
        Assert.Equal(10, aggregate.Project(now.AddDays(20)).OverallXp);
        Assert.True(aggregate.Schedule.Series.Single().Stopped);
    }

    /// <summary>Verifies losses are consented, category-locked, chronological and clamped without later debt.</summary>
    [Fact]
    public void PenaltyConsentAndZeroFloorDoNotCreateDebtOrReduceOtherTracks()
    {
        var now = At(2026, 9, 28);
        var aggregate = new QuestAggregate("UTC");
        var quest = new Quest(Guid.NewGuid().ToString(), "Commitment", "Achieved outcome", "c07", Rank.F, Effort.Medium, [], [], true, PenaltyPercent: 50);
        aggregate.Apply(new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0), now);
        Assert.Throws<ArgumentException>(() => aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: quest.Id, DueDate: "2026-09-28"), now));
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: quest.Id, DueDate: "2026-09-28", ConfirmPenalty: true, AcceptedLoss: 40, AcceptedQuest: quest), now);
        var penaltyId = aggregate.Occurrences.Single().Id;
        var after = now.AddDays(1);
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), after);
        aggregate.Apply(new(Guid.NewGuid(), "complete", aggregate.Occurrences.Last().Id), after);
        var state = aggregate.Project(after);
        Assert.Equal(0, state.Penalties.Single().ActualLoss);
        Assert.Equal(10, state.Categories.Single(c => c.Id == "c07").Xp);
        Assert.Equal(10, state.OverallXp);
        aggregate.Apply(new(Guid.NewGuid(), "abandon", penaltyId, ConfirmPenalty: true, AcceptedLoss: 40), after);
        Assert.Single(aggregate.Project(after).Penalties);
        Assert.Equal(10, aggregate.Project(after).Categories.Single(c => c.Id == "c07").Xp);
    }

    /// <summary>Verifies changed future terms suspend automatic consent and existing accepted locks survive edits.</summary>
    [Fact]
    public void PenaltyEditsPreserveAcceptedLossAndSuspendFutureAutoAcceptance()
    {
        var now = At(2026, 9, 28);
        var aggregate = new QuestAggregate("UTC");
        var quest = new Quest(Guid.NewGuid().ToString(), "Commitment", "Outcome", "c07", Rank.F, Effort.Small, [], [], true, PenaltyPercent: 50);
        aggregate.Apply(new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0), now);
        aggregate.Apply(new(Guid.NewGuid(), "save-series", QuestId: quest.Id, DueDate: "2026-09-28", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0, ConfirmPenalty: true, AcceptedLoss: 5, AcceptedQuest: quest), now);
        var id = aggregate.Occurrences.Single().Id;
        Assert.Throws<ArgumentException>(() => aggregate.Apply(new(Guid.NewGuid(), "save-definition", Definition: quest with { CategoryId = "c06" }, ExpectedRevision: 1), now));
        aggregate.Apply(new(Guid.NewGuid(), "save-definition", Definition: quest with { Effort = Effort.Medium }, ExpectedRevision: 1), now);
        Assert.Equal(5, aggregate.Occurrences.Single().Lifecycle!.LockedLoss);
        Assert.Equal(80, aggregate.Occurrences.Single().Quest.BaseXp);
        aggregate.Apply(new(Guid.NewGuid(), "pause", CategoryId: "c07"), now);
        Assert.Empty(aggregate.Project(now.AddDays(1)).Penalties);
        aggregate.Apply(new(Guid.NewGuid(), "resume", CategoryId: "c07"), now.AddDays(1));
        aggregate.Reconcile(now.AddDays(2));
        var offer = aggregate.Occurrences.Single(o => o.Id != id);
        Assert.True(offer.Lifecycle!.Unaccepted);
        Assert.Equal(QuestStatus.Offered, aggregate.Project(now.AddDays(2)).Occurrences.Single(o => o.Occurrence.Id == offer.Id).Status);
        aggregate.Apply(new(Guid.NewGuid(), "accept-offer", offer.Id, ConfirmPenalty: true, AcceptedLoss: 40, AcceptedQuest: offer.Quest), now.AddDays(2));
        Assert.Equal(40, aggregate.Occurrences.Single(o => o.Id == offer.Id).Lifecycle!.LockedLoss);
    }

    /// <summary>Consent cannot be replayed against a different outcome even when its penalty amount matches.</summary>
    [Fact]
    public void StalePenaltyConsentCannotAcceptEditedTerms()
    {
        var now = At(2026, 9, 28);
        var aggregate = new QuestAggregate("UTC");
        var quest = new Quest(Guid.NewGuid().ToString(), "Commitment", "Original outcome", "c07", Rank.F, Effort.Small, [], [], true, PenaltyPercent: 50);
        aggregate.Apply(new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0), now);
        aggregate.Apply(new(Guid.NewGuid(), "save-definition", Definition: quest with { Criterion = "Different outcome" }, ExpectedRevision: 1), now);
        Assert.Throws<ArgumentException>(() => aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: quest.Id, DueDate: "2026-09-28", ConfirmPenalty: true, AcceptedLoss: 5, AcceptedQuest: quest), now));
        Assert.Empty(aggregate.Occurrences);
    }

    private static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);
}
