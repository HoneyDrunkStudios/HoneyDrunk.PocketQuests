using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Tests.Fixtures;
using System.Globalization;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Real SQL reproductions of the independent lifecycle review findings.</summary>
/// <param name="fixture">Disposable independently deployed database.</param>
public sealed class RelationalReviewRegressionTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private TestQuestWorkflow Store => fixture.Commands();

    /// <summary>A current series quest cannot change through another action, without a newer revision, or without owned immutable configuration.</summary>
    /// <returns>Completion after rejecting unversioned and missing-configuration changes through real source reads after controlled same-owner corruption.</returns>
    [Fact]
    public async Task SeriesQuestPointerRejectsMismatchedAndMissingCommittedConfiguration()
    {
        var owner = new AccountIdentity("honeydrunk-identity", "usr_00000000000000000000000999");
        await Store.Initialize(owner, "Etc/UTC", Start);
        foreach (var quest in new[] { "PQ-CAT-Q01", "PQ-CAT-Q02" })
            await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest, DueDate: "2026-01-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0), Start);
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        var series = await db.QuestSeries.OrderBy(row => row.CreationOrdinal).Where(row => row.AccountId == account.Id).ToArrayAsync();
        var originalDefinition = series[0].QuestDefinitionId;
        await fixture.Execute($"UPDATE pocketquests.QuestSeries SET QuestDefinitionId='{series[1].QuestDefinitionId:D}' WHERE Id='{series[0].Id:D}';");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.Read(owner, Start));
        await fixture.Execute($"UPDATE pocketquests.QuestSeries SET QuestDefinitionId='{originalDefinition:D}', Revision=Revision+1 WHERE Id='{series[0].Id:D}';");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.Read(owner, Start));
        await fixture.Execute($"UPDATE pocketquests.QuestSeries SET Revision=Revision-1 WHERE Id='{series[0].Id:D}';");
        var recovered = await Store.Read(owner, Start);
        Assert.Equal(2, recovered.Schedule.Series.Length);
    }

    /// <summary>New deliveries link the exact terms/consent configuration; previously generated deliveries keep their original link.</summary>
    /// <returns>Completion after configuration provenance and historical receipt assertions.</returns>
    [Fact]
    public async Task DefinitionEditRetainsOriginalDeliveryLinksAndUsesNewConsentForLaterDeliveries()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var quest = Custom("Original terms");
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), Start);
        var series = Series(quest);
        var original = await Store.Execute(owner, series, Start);
        await using var db = fixture.Context();
        var first = await db.Set<QuestOccurrenceEntity>().SingleAsync(o => o.QuestSeriesId == series.SeriesId);
        var originalLink = first.QuestSeriesRevisionId;
        var edit = new QuestCommand(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest with { Title = "Edited terms", PenaltyPercent = 25 }, ExpectedRevision: 1);
        var edited = await Store.Execute(owner, edit, Start.AddDays(1));
        await Store.Reconcile(owner, Start.AddDays(2));
        var deliveries = await db.Set<QuestOccurrenceEntity>().Where(o => o.QuestSeriesId == series.SeriesId).OrderBy(o => o.CreationOrdinal).ToListAsync();
        var configurations = await db.Set<QuestSeriesRevisionEntity>().Where(r => r.QuestSeriesId == series.SeriesId).OrderBy(r => r.Revision).ToListAsync();
        await Evidence("review-series-configuration.json", new { links = deliveries.Select(d => d.QuestSeriesRevisionId), configurations = configurations.Select(c => new { c.Id, c.Revision, c.ScheduleVersion, c.QuestDefinitionRevisionId, c.HasAutoAcceptPenalty }) });
        Assert.Equal(2, configurations.Count);
        Assert.Equal(originalLink, deliveries[0].QuestSeriesRevisionId);
        Assert.Equal(originalLink, deliveries[1].QuestSeriesRevisionId);
        Assert.Equal(configurations[1].Id, deliveries[2].QuestSeriesRevisionId);
        Assert.Equal(deliveries[2].QuestDefinitionRevisionId, configurations[1].QuestDefinitionRevisionId);
        Assert.True(configurations[0].HasAutoAcceptPenalty);
        Assert.False(configurations[1].HasAutoAcceptPenalty);
        Assert.Null(deliveries[2].AcceptedAt);
        Equal(original, await Store.Execute(owner, series, Start.AddYears(1)));
        Equal(edited, await Store.Execute(owner, edit, Start.AddYears(1)));
    }

    /// <summary>Pre-pause missed dates and penalties survive delayed lifecycle arrival and bounded resume continuation.</summary>
    /// <param name="days">Number of days without materialization before the pause.</param>
    /// <returns>Completion after date/penalty/budget/replay assertions.</returns>
    [Theory]
    [InlineData(3)]
    [InlineData(400)]
    public async Task LifecyclePauseRetainsOverdueHistoryBeforeShiftingFutureDates(int days)
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var quest = Custom("Penalty series");
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), Start);
        var series = Series(quest);
        var original = await Store.Execute(owner, series, Start);
        var pausedAt = Start.AddDays(days);
        var resumedAt = pausedAt.AddDays(6);
        await fixture.Lifecycle().ReceiveLifecycle(Intent(owner, 1, IdentityProtocol.Inactive, pausedAt, pausedAt), "private-ack", pausedAt);
        await fixture.Lifecycle().ReceiveLifecycle(Intent(owner, 2, IdentityProtocol.Active, resumedAt, pausedAt), "private-ack", resumedAt);
        var resume = new QuestCommand(Guid.NewGuid(), QuestActions.Resume);
        var pending = 0;
        QuestState? result = null;
        await using var db = fixture.Context();
        while (result is null)
        {
            try
            {
                result = await Store.Execute(owner, resume, resumedAt);
            }
            catch (ReconciliationPendingException)
            {
                Assert.InRange(++pending, 1, 4);
                Assert.False(await db.Set<CommandReceiptEntity>().AnyAsync(r => r.Id == resume.OperationId));
                Assert.True((await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject)).IsAccountPaused);
            }
        }

        var expectedDates = Enumerable.Range(0, days).Select(day => Start.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(resumedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToArray();
        var dates = result.Occurrences.Select(o => o.Occurrence.DueDate).ToArray();
        await Evidence($"review-pause-{days}.json", new { expectedDates, actualDates = dates, pending, penalties = result.Penalties });
        Assert.Equal(expectedDates, dates);
        Assert.Equal(days, result.Penalties.Length);
        Assert.Equal(Enumerable.Range(1, days).Select(day => Start.AddDays(day).Date), result.Penalties.Select(p => p.At.Date));
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var batches = await db.Set<QuestCommandHistoryEntity>().Where(h => h.AccountId == account.Id && h.ActionCode == "$reconcile").ToListAsync();
        Assert.All(batches, h => Assert.InRange(h.ReconciliationLimit, 1, 100));
        Assert.Equal(days == 400 ? 3 : 0, pending);
        Equal(result, await Store.Execute(owner, resume, resumedAt.AddYears(1)));
        Equal(original, await Store.Execute(owner, series, resumedAt.AddYears(1)));
    }

    /// <summary>The existing save-series command may select another quest while older configuration and occurrence history remains intact.</summary>
    /// <returns>Completion after supported domain behavior crosses the controlled SQL writer.</returns>
    [Fact]
    public async Task ExistingSeriesCanSelectAnotherQuestWithoutRewritingOldDeliveries()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var first = new QuestCommand(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q01", DueDate: "2026-01-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0);
        var original = await Store.Execute(owner, first, Start);
        var change = new QuestCommand(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q02", DueDate: "2026-01-02", SeriesId: first.SeriesId, Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 1);
        var changed = await Store.Execute(owner, change, Start.AddDays(1));
        Assert.Equal("PQ-CAT-Q02", Assert.Single(changed.Schedule.Series).Quest.Id);
        Assert.Contains(changed.Occurrences, o => o.Occurrence.Quest.Id == "PQ-CAT-Q01");
        Assert.Contains(changed.Occurrences, o => o.Occurrence.Quest.Id == "PQ-CAT-Q02");
        Equal(original, await Store.Execute(owner, first, Start.AddYears(1)));
        Equal(changed, await Store.Execute(owner, change, Start.AddYears(1)));
    }

    private static AccountIdentity Identity() => new("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());

    private static Quest Custom(string title) => new(Guid.NewGuid().ToString("D"), title, "Finish work", "c01", Rank.F, Effort.Small, [], [], true, PenaltyPercent: 50);

    private static QuestCommand Series(Quest quest) => new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest.Id, DueDate: "2026-01-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0, ConfirmPenalty: true, AcceptedLoss: 5, AcceptedQuest: quest);

    private static LifecycleIntent Intent(AccountIdentity owner, long version, string state, DateTimeOffset effective, DateTimeOffset pause) => new(owner.Subject, version, state, effective, effective.AddHours(1), IdentityProtocol.ConsumerId, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), pause);

    private static void Equal(QuestState expected, QuestState actual) => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    private static Task Evidence(string name, object value) => File.WriteAllTextAsync(Path.Combine(Environment.GetEnvironmentVariable("POCKETQUESTS_SCHEMA_EVIDENCE")!, name), JsonSerializer.Serialize(value));
}
