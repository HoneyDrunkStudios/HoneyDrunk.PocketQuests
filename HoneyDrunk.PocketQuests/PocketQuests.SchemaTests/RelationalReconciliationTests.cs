using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Services.Quests;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Bounded recurrence retains old deliveries, exact receipts and unconsumed pending proofs.</summary>
/// <param name="fixture">Only the independently deployed disposable database.</param>
public sealed class RelationalReconciliationTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private IQuestService Store => fixture.Commands();

    /// <summary>Splitting a globally ordered recurrence queue preserves every cursor, delivery and projection.</summary>
    [Fact]
    public void BoundedDomainStepsMatchUnboundedAcrossSeriesAndZeroActionBudget()
    {
        var whole = new QuestAggregate("Etc/UTC");
        var bounded = new QuestAggregate("Etc/UTC");
        for (var index = 0; index < 3; index++)
        {
            var command = Series();
            Assert.True(whole.Apply(command, Start, 0).HasMore);
            Assert.True(bounded.Apply(command, Start, 0).HasMore);
        }

        var later = Start.AddDays(400);
        whole.Reconcile(later);
        var count = 0;
        ReconciliationProgress progress;
        do
        {
            progress = bounded.Reconcile(later, 17);
            Assert.InRange(progress.Processed, 1, 17);
            count += progress.Processed;
        }
        while (progress.HasMore);
        Assert.Equal(1203, count);
        Equal(whole.Project(later), bounded.Project(later));
        Assert.Equal(new ReconciliationProgress(0, false), bounded.Reconcile(later, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => bounded.Reconcile(later, -1));
    }

    /// <summary>A four-hundred-day backlog commits limited source batches before the identical offline command consumes its proof.</summary>
    /// <returns>Completion after current/receipt parity, proof ordering and unchanged read-rowversion assertions.</returns>
    [Fact]
    public async Task PendingCommandDrainsBacklogWithoutReceiptOrProofConsumption()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var series = Series();
        var first = await Store.Execute(owner, series, Start);
        var domain = new QuestAggregate("Etc/UTC");
        domain.Apply(series, Start);
        var occurrence = Assert.Single(first.Occurrences).Occurrence.Id;
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence, RecordedTime: new(anchor.Id, anchor.BootId, 1, 1000.25, Start.AddMilliseconds(1000.25)));
        var later = Start.AddDays(400);
        await using var db = fixture.Context();
        var before = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        await Assert.ThrowsAsync<ReconciliationPendingException>(() => Store.Read(owner, later));
        Assert.Equal(before.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id)).RowVersion);
        for (var batch = 1; batch <= 3; batch++)
        {
            await Assert.ThrowsAsync<ReconciliationPendingException>(() => Store.Execute(owner, command, later));
            var saved = await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id);
            Assert.Equal(0, saved.LastOrdinal);
            Assert.Equal(0, saved.LastElapsedMilliseconds);
            Assert.False(await db.Set<CommandReceiptEntity>().AnyAsync(r => r.Id == command.OperationId));
            Assert.False(await db.Set<QuestCommandHistoryEntity>().AnyAsync(r => r.Id == command.OperationId));
            Assert.Equal(1 + (100 * batch), await db.Set<QuestOccurrenceEntity>().CountAsync(o => o.AccountId == before.Id));
            var page = await Store.ReadOccurrences(owner, 0, 1, later);
            Assert.True(page.HasPendingReconciliation);
            Assert.Equal(Start, page.ProjectionAsOfAt);
        }

        var result = await Store.Execute(owner, command, later);
        domain.Reconcile(later);
        domain.Apply(command, Start.AddMilliseconds(1000.25));
        Equal(domain.Project(later), result with { CompletionOutcome = null });
        var committed = await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id);
        Assert.False(committed.HasPendingReconciliation);
        Assert.Equal(later, committed.ProjectionAsOfAt);
        Assert.Equal(5, committed.MutationVersion);
        Assert.Equal(2, await db.Set<CommandReceiptEntity>().CountAsync(r => r.AccountId == before.Id));
        var history = await db.Set<QuestCommandHistoryEntity>().Where(r => r.AccountId == before.Id && r.ActionCode == "$reconcile").ToListAsync();
        Assert.Equal(3, history.Count);
        Assert.All(history, row => Assert.Null(row.CommandReceiptId));
        Assert.All(history, row => Assert.Equal(100, row.ReconciliationLimit));
        Assert.Equal(1, (await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id)).LastOrdinal);
        Equal(result, await Store.Execute(owner, command, later.AddYears(1)));
        Equal(first, await Store.Execute(owner, series, later.AddYears(1)));
    }

    /// <summary>Issuance cannot expose a partial backlog as an anchored session; maintenance finishes without any client receipts.</summary>
    /// <returns>Completion after issuance fencing, bounded maintenance and replay assertions.</returns>
    [Fact]
    public async Task AnchorWaitsForRetainedBacklogAndMaintenanceDoesNotWriteOnReads()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var command = Series();
        var original = await Store.Execute(owner, command, Start);
        var later = Start.AddDays(365);
        await Assert.ThrowsAsync<ReconciliationPendingException>(() => Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), later, later));
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        Assert.False(await db.Set<SyncAnchorEntity>().AnyAsync(a => a.AccountId == account.Id));
        var processed = 0;
        ReconciliationProgress progress;
        do
        {
            progress = await Store.Reconcile(owner, later, 37);
            Assert.InRange(progress.Processed, 1, 37);
            processed += progress.Processed;
        }
        while (progress.HasMore);
        Assert.Equal(265, processed);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), later, later);
        Assert.Equal(later, anchor.RecordedTimeFloor);
        Assert.Equal(1, await db.Set<CommandReceiptEntity>().CountAsync(r => r.AccountId == account.Id));
        var beforeRead = await db.Set<AccountEntity>().SingleAsync(a => a.Id == account.Id);
        Assert.Equal(366, (await Store.Read(owner, later)).Occurrences.Length);
        Assert.Equal(beforeRead.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == account.Id)).RowVersion);
        Equal(original, await Store.Execute(owner, command, later.AddYears(1)));
    }

    /// <summary>A very late series creation retains its original response while subsequent maintenance delivers every older due occurrence.</summary>
    /// <returns>Completion after bounded deferred delivery and original response reconstruction.</returns>
    [Fact]
    public async Task LateSeriesCreationRetainsOriginalResponseAndAllDueDeliveries()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var command = Series() with { RecordedTime = new(anchor.Id, anchor.BootId, 1, 1000, Start.AddSeconds(1)) };
        var later = Start.AddDays(400);
        var original = await Store.Execute(owner, command, later);
        Assert.Single(original.Occurrences);
        Assert.True((await Store.ReadOccurrences(owner, 0, 10, later)).HasPendingReconciliation);
        for (var index = 0; index < 4; index++)
        {
            var step = await Store.Reconcile(owner, later);
            Assert.Equal(100, step.Processed);
            Assert.Equal(index < 3, step.HasMore);
        }

        var domain = new QuestAggregate("Etc/UTC");
        domain.Apply(command, Start.AddSeconds(1));
        domain.Reconcile(later);
        Equal(domain.Project(later), await Store.Read(owner, later));
        Equal(original, await Store.Execute(owner, command, later.AddYears(1)));
        await using var db = fixture.Context();
        Assert.Equal(1, (await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id)).LastOrdinal);
    }

    private static AccountIdentity Identity() => new("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());

    private static QuestCommand Series() => new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q01", DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1);

    private static void Equal(QuestState expected, QuestState actual) => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
}
