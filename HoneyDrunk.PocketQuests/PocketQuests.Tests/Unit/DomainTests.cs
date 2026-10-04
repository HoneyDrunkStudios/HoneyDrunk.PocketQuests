using NodaTime;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Tests.Unit;

/// <summary>Executable regression coverage for domain tests.</summary>
public class DomainTests
{
    /// <summary>Verifies starter catalog preserves exact mappings and no forced productivity.</summary>
    [Fact]
    public void StarterCatalogPreservesExactMappingsAndNoForcedProductivity()
    {
        Assert.Equal(10, Catalog.Categories.Length);
        Assert.Equal(8, Catalog.Attributes.Length);
        Assert.Equal(18, Catalog.Skills.Length);
        Assert.Equal(10, Catalog.Quests.Length);
        Assert.All(Catalog.Quests, q => Assert.Equal(10, q.BaseXp));
        Assert.Equal(8, Progression.Allocate(10, Catalog.Quests[0].Attributes)["a01"]);

        // Dexterity precedes Creativity in stable ID order and wins the 0.5 tie.
        Assert.Equal(3, Progression.Allocate(10, Catalog.Quests[2].Attributes)["a04"]);
        Assert.All(new[] { 5, 6, 9 }, i =>
        {
            Assert.Empty(Catalog.Quests[i].Skills);
            Assert.Empty(Catalog.Quests[i].Attributes);
        });
    }

    /// <summary>Verifies rewards.</summary>
    /// <param name="rank">The rank test case value.</param>
    /// <param name="small">The small test case value.</param>
    /// <param name="medium">The medium test case value.</param>
    /// <param name="large">The large test case value.</param>
    [Theory]
    [InlineData(Rank.F, 10, 80, 800)]
    [InlineData(Rank.E, 13, 104, 1040)]
    [InlineData(Rank.D, 17, 136, 1360)]
    [InlineData(Rank.C, 22, 176, 1760)]
    [InlineData(Rank.B, 28, 224, 2240)]
    [InlineData(Rank.A, 35, 280, 2800)]
    [InlineData(Rank.S, 43, 344, 3440)]
    public void Rewards(Rank rank, int small, int medium, int large)
    {
        Assert.Equal(small, Progression.Reward(rank, Effort.Small));
        Assert.Equal(medium, Progression.Reward(rank, Effort.Medium));
        Assert.Equal(large, Progression.Reward(rank, Effort.Large));
    }

    /// <summary>Verifies every level boundary.</summary>
    /// <param name="track">The track test case value.</param>
    [Theory]
    [InlineData(Track.Overall)]
    [InlineData(Track.Category)]
    [InlineData(Track.Attribute)]
    [InlineData(Track.Skill)]
    public void EveryLevelBoundary(Track track)
    {
        Assert.Equal(1, Progression.Level(0, track));
        for (var level = 2; level <= 200; level++)
        {
            var threshold = Progression.Threshold(level, track);
            Assert.Equal(level - 1, Progression.Level(threshold - 1, track));
            Assert.Equal(level, Progression.Level(threshold, track));
        }
    }

    /// <summary>Verifies allocations conserve pool and reject duplicate or invalid weights.</summary>
    [Fact]
    public void AllocationsConservePoolAndRejectDuplicateOrInvalidWeights()
    {
        Assert.Equal(60, Progression.Allocate(80, [new("s01", 7500), new("s02", 2500)])["s01"]);
        for (var xp = 0; xp < 100; xp++)
            Assert.Equal(xp, Progression.Allocate(xp, [new("a", 3333), new("b", 3333), new("c", 3334)]).Values.Sum());
        Assert.Empty(Progression.Allocate(80, []));
        Assert.Throws<QuestValidationException>(() => Progression.Allocate(80, [new("a", 5000), new("a", 5000)]));
        Assert.Throws<QuestValidationException>(() => Progression.Allocate(80, [new("a", 9999)]));
    }

    /// <summary>Verifies specialist access ignores global rank but checks zero weight skills.</summary>
    [Fact]
    public void SpecialistAccessIgnoresGlobalRankButChecksZeroWeightSkills()
    {
        var quest = Catalog.Quests[3] with { Rank = Rank.A, Skills = [new("s07", 10000)] };
        var skills = new Dictionary<string, long> { ["s07"] = Progression.Seed(Experience.Expert) };
        Assert.True(Progression.Eligible(quest, new Dictionary<string, long>(), skills));
        Assert.False(Progression.Eligible(quest with { Rank = Rank.S }, new Dictionary<string, long>(), skills));
        Assert.False(Progression.Eligible(quest with { Skills = [new("s07", 10000), new("s08", 0)] }, new Dictionary<string, long>(), skills));
        Assert.False(Progression.Eligible(quest with { Skills = [] }, new Dictionary<string, long>(), skills));
    }

    /// <summary>Verifies every global rank requires count floor and total.</summary>
    [Fact]
    public void EveryGlobalRankRequiresCountFloorAndTotal()
    {
        foreach (var rule in Progression.Rules.Skip(1))
        {
            var balances = Catalog.Categories.ToDictionary(c => c.Id, _ => 0L);
            for (var i = 0; i < rule.Count; i++)
                balances[Catalog.Categories[i].Id] = rule.Floor;
            balances["c01"] += rule.Total - balances.Values.Sum();
            Assert.Equal(rule.Rank, Progression.GlobalRank(balances).Current);
            balances["c01"]--;
            Assert.True(Progression.GlobalRank(balances).Current < rule.Rank);
            balances["c01"] += 1000000;
            balances[Catalog.Categories[rule.Count - 1].Id] = rule.Floor - 1;
            Assert.True(Progression.GlobalRank(balances).Current < rule.Rank);
        }
    }

    /// <summary>Verifies recurrence retains original month and leap anchors and pause offset.</summary>
    [Fact]
    public void RecurrenceRetainsOriginalMonthAndLeapAnchorsAndPauseOffset()
    {
        var jan = new LocalDate(2027, 1, 31);
        Assert.Equal(new LocalDate(2027, 2, 28), Scheduling.Recurrence(jan, Cadence.Months, 1, 1));
        Assert.Equal(new LocalDate(2027, 3, 31), Scheduling.Recurrence(jan, Cadence.Months, 1, 2));
        Assert.Equal(new LocalDate(2027, 3, 1), Scheduling.Recurrence(jan, Cadence.Months, 1, 1, 1));
        Assert.Equal(new LocalDate(2027, 4, 1), Scheduling.Recurrence(jan, Cadence.Months, 1, 2, 1));
        var leap = new LocalDate(2024, 2, 29);
        Assert.Equal(new LocalDate(2025, 2, 28), Scheduling.Recurrence(leap, Cadence.Years, 1, 1));
        Assert.Equal(new LocalDate(2028, 2, 29), Scheduling.Recurrence(leap, Cadence.Years, 1, 4));
        Assert.Throws<QuestValidationException>(() => Scheduling.Recurrence(jan, Cadence.Days, 0, 1));
    }

    /// <summary>Verifies actual timezone database resolves cutoffs.</summary>
    /// <param name="due">The due test case value.</param>
    /// <param name="zone">The zone test case value.</param>
    /// <param name="expected">The expected test case value.</param>
    [Theory]
    [InlineData("2026-03-08", "America/New_York", "2026-03-09T04:00:00Z")]
    [InlineData("2026-11-01", "America/New_York", "2026-11-02T05:00:00Z")]
    [InlineData("2026-03-08", "Asia/Kolkata", "2026-03-08T18:30:00Z")]
    [InlineData("2011-12-29", "Pacific/Apia", "2011-12-30T10:00:00Z")]
    public void ActualTimezoneDatabaseResolvesCutoffs(string due, string zone, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Scheduling.Deadline(Scheduling.ParseDate(due), zone));

    /// <summary>Verifies complete undo recomplete has one grant and stale undo cannot reverse it.</summary>
    [Fact]
    public void CompleteUndoRecompleteHasOneGrantAndStaleUndoCannotReverseIt()
    {
        var now = DateTimeOffset.Parse("2026-09-28T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var a = new QuestAggregate("UTC");
        a.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01", DueDate: "2026-09-28"), now);
        var id = a.Occurrences.Single().Id;
        a.Apply(new(Guid.NewGuid(), "complete", id), now);
        a.Apply(new(Guid.NewGuid(), "complete", id), now);
        Assert.Equal(10, a.Project(now).OverallXp);
        var undo = new QuestCommand(Guid.NewGuid(), "undo", id, CompletionId: a.Completions.Single().Id);
        a.Apply(undo, now.AddMinutes(1));
        Assert.Equal(0, a.Project(now.AddMinutes(1)).OverallXp);
        a.Apply(new(Guid.NewGuid(), "complete", id), now.AddMinutes(2));
        a.Apply(undo with { OperationId = Guid.NewGuid() }, now.AddMinutes(3));
        Assert.Equal(10, a.Project(now.AddMinutes(3)).OverallXp);
        Assert.Equal(2, a.Completions.Count);
    }

    /// <summary>Verifies exact cutoff rejects completion and post deadline undo reopens missed.</summary>
    [Fact]
    public void ExactCutoffRejectsCompletionAndPostDeadlineUndoReopensMissed()
    {
        var now = DateTimeOffset.Parse("2026-09-28T23:59:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var a = new QuestAggregate("UTC");
        a.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28"), now);
        var id = a.Occurrences.Single().Id;
        Assert.Throws<QuestConflictException>(() => a.Apply(new(Guid.NewGuid(), "complete", id), now.AddMinutes(1)));
        a.Apply(new(Guid.NewGuid(), "complete", id), now);
        a.Apply(new(Guid.NewGuid(), "undo", id, CompletionId: a.Completions.Single().Id), now.AddMinutes(2));
        Assert.Equal(QuestStatus.Missed, a.Project(now.AddMinutes(2)).Occurrences.Single().Status);
        Assert.Equal(0, a.Project(now.AddMinutes(2)).OverallXp);
    }

    /// <summary>Verifies undo expires at exactly24 elapsed hours.</summary>
    [Fact]
    public void UndoExpiresAtExactly24ElapsedHours()
    {
        var now = DateTimeOffset.Parse("2026-03-08T06:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var a = new QuestAggregate("America/New_York");
        a.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
        var id = a.Occurrences.Single().Id;
        a.Apply(new(Guid.NewGuid(), "complete", id), now);
        Assert.Throws<QuestConflictException>(() => a.Apply(new(Guid.NewGuid(), "undo", id, CompletionId: a.Completions.Single().Id), now.AddHours(24)));
        Assert.False(a.Project(now.AddHours(24)).Occurrences.Single().CanUndo);
    }

    /// <summary>Verifies streaks and dependent bonuses rebuild after undo.</summary>
    [Fact]
    public void StreaksAndDependentBonusesRebuildAfterUndo()
    {
        var start = DateTimeOffset.Parse("2026-09-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var a = new QuestAggregate("UTC");
        for (var day = 0; day < 21; day++)
        {
            a.Apply(new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), start.AddDays(day));
            a.Apply(new(Guid.NewGuid(), "complete", a.Occurrences.Last().Id), start.AddDays(day));
        }

        var state = a.Project(start.AddDays(20));
        Assert.Equal(210, state.OverallXp);
        Assert.Equal(21, state.Streaks.Single(s => s.CategoryId == "c07").Days);
        Assert.Equal(20, state.Streaks.Single(s => s.CategoryId == "c07").Rate);
        var last = a.Completions.Last();
        a.Apply(new(Guid.NewGuid(), "undo", last.OccurrenceId, CompletionId: last.Id), start.AddDays(20).AddMinutes(1));
        Assert.Equal(200, a.Project(start.AddDays(20).AddMinutes(1)).OverallXp);
        Assert.Equal(state.Categories.Single(c => c.Id == "c07").Xp - 12, a.Project(start.AddDays(20).AddMinutes(1)).Categories.Single(c => c.Id == "c07").Xp);
    }

    /// <summary>Verifies all six entitlement thresholds and rank gates.</summary>
    [Fact]
    public void AllSixEntitlementThresholdsAndRankGates()
    {
        var cases = new[]
        {
            ("A01", Catalog.Quests[0] with { IsCustom = true }, 1, rank: Rank.F), ("A02", Catalog.Quests[6], 5, rank: Rank.F),
            ("B01", Catalog.Quests[9], 3, rank: Rank.F), ("B02", Catalog.Quests[5], 10, rank: Rank.F),
            ("F01", Catalog.Quests[1], 10, rank: Rank.E), ("F02", Catalog.Quests[6], 10, rank: Rank.D)
        };
        foreach (var (id, quest, threshold, rank) in cases)
        {
            Assert.False(Progression.Entitlements(Enumerable.Repeat(quest, threshold - 1), rank).Single(e => e.Id == "PQ-CAT-" + id).Earned);
            Assert.True(Progression.Entitlements(Enumerable.Repeat(quest, threshold), rank).Single(e => e.Id == "PQ-CAT-" + id).Earned);
            Assert.True(Progression.Entitlements(Enumerable.Repeat(quest, threshold + 1), rank).Single(e => e.Id == "PQ-CAT-" + id).Earned);
            if (rank > Rank.F)
                Assert.False(Progression.Entitlements(Enumerable.Repeat(quest, threshold), Rank.F).Single(e => e.Id == "PQ-CAT-" + id).Earned);
        }
    }
}
