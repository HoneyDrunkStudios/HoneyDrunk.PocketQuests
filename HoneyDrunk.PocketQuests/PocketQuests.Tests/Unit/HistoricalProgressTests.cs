using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Tests.Unit;

/// <summary>Regression coverage for deliveries evaluated at their effective time.</summary>
public sealed class HistoricalProgressTests
{
    /// <summary>Later placements and earned rewards cannot qualify earlier recurring deliveries.</summary>
    [Fact]
    public void ReconciliationDoesNotUseFuturePlacementOrCompletion()
    {
        var start = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var quest = Catalog.Quests[3] with { Rank = Rank.D, Skills = [new("s07", 10000)] };
        var series = new QuestSeries(Guid.NewGuid(), quest, "2026-09-02", Cadence.Days, 1, EffectiveAt: start);
        var aggregate = new QuestAggregate("UTC", schedule: ScheduleState.Empty with { Series = [series] });
        aggregate.Apply(new(Guid.NewGuid(), "assess-skill", SkillId: "s07", Experience: Experience.Expert), start.AddDays(2));
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q04"), start.AddDays(2));
        aggregate.Apply(new(Guid.NewGuid(), "complete", aggregate.Occurrences.Single().Id), start.AddDays(2));
        aggregate.Reconcile(start.AddDays(3));
        Assert.Equal(0, aggregate.Project(start.AddDays(1)).OverallXp);
        Assert.Equal(0, aggregate.Project(start.AddDays(1)).Skills.Single(s => s.Id == "s07").Xp);
        var deliveries = aggregate.Occurrences.Where(o => o.Lifecycle?.SeriesId == series.Id).OrderBy(o => o.DueDate).ToArray();
        Assert.Equal(3, deliveries.Length);
        Assert.True(deliveries[0].Lifecycle!.Unaccepted);
        Assert.True(deliveries[1].Lifecycle!.Unaccepted);
        Assert.False(deliveries[2].Lifecycle!.Unaccepted);
    }

    /// <summary>A later reversal changes current totals without rewriting the earlier eligibility projection.</summary>
    [Fact]
    public void HistoricalProjectionHonorsUndoEffectiveTime()
    {
        var at = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var aggregate = new QuestAggregate("UTC");
        aggregate.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), at);
        var id = aggregate.Occurrences.Single().Id;
        aggregate.Apply(new(Guid.NewGuid(), "complete", id), at);
        aggregate.Apply(new(Guid.NewGuid(), "undo", id, CompletionId: aggregate.Completions.Single().Id), at.AddMinutes(5));
        Assert.Equal(10, aggregate.Project(at).OverallXp);
        Assert.Equal(0, aggregate.Project(at.AddMinutes(5)).OverallXp);
    }
}
