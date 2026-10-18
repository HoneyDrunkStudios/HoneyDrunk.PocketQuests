using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Attributes;
using PocketQuests.Data.DataServices.Categories;
using PocketQuests.Data.DataServices.Progress;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.DataServices.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Tests.Fixtures;

namespace PocketQuests.Tests.Integration;

/// <summary>Real SQL query contracts shared by the package and Identity-source test lanes.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Typed reads preserve account ownership, tracked identity and cancellation after real commands.</summary>
    /// <returns>Completion after reading two independently populated accounts.</returns>
    [Fact]
    public async Task TypedQueries_PreserveAccountIsolationTrackingAndCancellation()
    {
        await SeedQueryAccount("query-owner");
        await SeedQueryAccount("query-neighbor");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Connection).Options);
        var accounts = await db.Account.Select(row => row.Id).ToArrayAsync();
        Assert.Equal(2, accounts.Length);

        await AssertAccountQuery(db, accounts, new AccountAuditRecordDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new AccountInterestDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new AccountPauseDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new TimeZoneChangeDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new CategoryProgressDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new XpBalanceDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new XpLedgerEntryDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestCommandHistoryDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestCommandInterestDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestCompletionDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestDefinitionDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestDefinitionRevisionDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestDefinitionAttributeAllocationDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestDefinitionSkillAllocationDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestOccurrenceDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestOccurrenceEventDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestOccurrenceRevisionDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestSeriesDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new QuestSeriesRevisionDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new CustomSkillDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new SkillAssessmentDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new CommandReceiptDataService(db).GetByAccountId, row => row.AccountId);
        await AssertAccountQuery(db, accounts, new SyncAnchorDataService(db).GetByAccountId, row => row.AccountId);

        var categories = await new CategoryDataService(db).GetCatalog();
        Assert.Contains(categories, row => row.Id == "c01");
        Assert.Equal(categories.Select(row => row.SortOrder).Order(), categories.Select(row => row.SortOrder));
        var attributes = await new AttributeDataService(db).GetCatalog();
        Assert.Contains(attributes, row => row.Id == "a01");
        Assert.Equal(attributes.Select(row => row.SortOrder).Order(), attributes.Select(row => row.SortOrder));
        var skills = await new SkillDataService(db).GetCatalog();
        Assert.Contains(skills, row => row.Id == "s01");
        Assert.Equal(skills.Select(row => row.SortOrder).Order(), skills.Select(row => row.SortOrder));
        Assert.Contains(await new SystemQuestDataService(db).GetCatalog(), row => row.Id == "PQ-CAT-Q01");
    }

    private static async Task AssertAccountQuery<TEntity>(AppDbContext db, Guid[] accounts, Func<Guid, CancellationToken, Task<IReadOnlyList<TEntity>>> query, Func<TEntity, Guid> owner)
        where TEntity : class
    {
        foreach (var account in accounts)
        {
            var rows = await query(account, CancellationToken.None);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Equal(account, owner(row)));
            Assert.All(rows, row => Assert.Equal(EntityState.Unchanged, db.Entry(row).State));
            var repeated = await query(account, CancellationToken.None);
            Assert.Equal(rows.Count, repeated.Count);
            Assert.All(rows, row => Assert.Contains(repeated, candidate => ReferenceEquals(row, candidate)));
        }

        Assert.Empty(await query(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query(accounts[0], new CancellationToken(canceled: true)));
    }

    private async Task SeedQueryAccount(string subject)
    {
        await using var scope = Services().CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<TestQuestWorkflow>();
        var identity = TestIdentity(subject);
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        await workflow.Initialize(identity, "UTC", now);
        await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.Interests, Interests: ["c01"]), now);
        await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: Guid.NewGuid().ToString(), SkillName: "Personal practice", ExpectedRevision: 0), now);
        await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.AssessSkill, SkillId: "s01", Experience: Experience.Practiced), now);
        var quest = new Quest(Guid.NewGuid().ToString(), "Private practice", "Finish a practice", "c01", Rank.F, Effort.Small, [new("a01", 10000)], [new("s01", 10000)], true);
        await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), now);
        var scheduled = await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest.Id, DueDate: "2026-01-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0), now);
        await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.Complete, scheduled.Occurrences.Single().Occurrence.Id), now);
        await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.Pause, CategoryId: "c02"), now);
        await workflow.Execute(identity, new(Guid.NewGuid(), QuestActions.Zone, NewZone: "America/New_York", ExpectedZone: "UTC", ConfirmZoneChange: true), now);
        await workflow.CreateAnchor(identity, Guid.NewGuid(), Guid.NewGuid(), now, now);
    }
}
