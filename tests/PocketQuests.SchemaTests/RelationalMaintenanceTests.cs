using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;

namespace PocketQuests.SchemaTests;

/// <summary>Private worker paging and concurrent bounded steps preserve fairness, lifecycle fences and idle row versions.</summary>
/// <param name="fixture">Independent disposable database for a known candidate population.</param>
public sealed class RelationalMaintenanceTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>A small page cannot starve later accounts, concurrent workers cannot duplicate deliveries, and idle profiles are not rewritten every tick.</summary>
    /// <returns>Completion after cursor rounds and parallel independent service instances.</returns>
    [Fact]
    public async Task BoundedMaintenanceRotatesAccountsAndSerializesConcurrentDeliveries()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var later = start.AddDays(400);
        var store = fixture.Commands();
        var owners = Enumerable.Range(0, 3).Select(_ => Identity()).ToArray();
        foreach (var owner in owners)
        {
            await store.Initialize(owner, "Etc/UTC", start);
            await store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q01", DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1), start);
        }

        var idle = Identity();
        await store.Initialize(idle, "Etc/UTC", start);
        await store.Execute(idle, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01"), start);
        var inactive = Identity();
        await store.Initialize(inactive, "Etc/UTC", start);
        await store.Execute(inactive, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01"), start);
        await fixture.Lifecycle().ReceiveLifecycle(new LifecycleIntent(inactive.Subject, 1, IdentityProtocol.Inactive, start, start.AddHours(1), IdentityProtocol.ConsumerId, new string('a', 64), start), "private-ack", start);
        await using var db = fixture.Context();
        var fenced = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == inactive.Subject);
        ReconciliationCursor? cursor = null;
        var scanned = 0;
        var steps = 0;
        do
        {
            var batch = await store.ReconcileAccounts(later, cursor, 1, 37);
            Assert.InRange(batch.Scanned, 0, 1);
            scanned += batch.Scanned;
            steps += batch.Deliveries;
            cursor = batch.Next;
            Assert.InRange(scanned, 1, 4);
        }
        while (cursor is not null);
        Assert.Equal(4, scanned);
        Assert.Equal(111, steps);
        foreach (var owner in owners)
        {
            var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
            Assert.True(account.HasPendingReconciliation);
            Assert.Equal(38, await db.Set<QuestOccurrenceEntity>().CountAsync(o => o.AccountId == account.Id));
        }

        var idleBefore = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == idle.Subject);
        var concurrent = await Task.WhenAll(fixture.Commands().ReconcileAccounts(later.AddMinutes(1), maximumAccounts: 50, maximumDeliveries: 37), fixture.Commands().ReconcileAccounts(later.AddMinutes(1), maximumAccounts: 50, maximumDeliveries: 37));
        Assert.Equal(222, concurrent.Sum(b => b.Deliveries));
        foreach (var owner in owners)
        {
            var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
            Assert.Equal(112, await db.Set<QuestOccurrenceEntity>().CountAsync(o => o.AccountId == account.Id));
            Assert.Equal(1, await db.Set<CommandReceiptEntity>().CountAsync(r => r.AccountId == account.Id));
        }

        Assert.Equal(idleBefore.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == idleBefore.Id)).RowVersion);
        Assert.Equal(fenced.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == fenced.Id)).RowVersion);
        await store.Reconcile(idle, later.AddMinutes(2));
        Assert.Equal(idleBefore.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == idleBefore.Id)).RowVersion);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ReconcileAccounts(later, maximumAccounts: 51));
    }

    private static AccountIdentity Identity() => new("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
}
