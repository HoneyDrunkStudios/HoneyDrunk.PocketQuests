using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.Repositories;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Tests.Fixtures;
using System.Text.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Exercises clock ordering through persisted anchors, command receipts and fresh store instances.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>A refreshed anchor cannot place immediate Undo before the completion already shown to the client.</summary>
    /// <param name="leadSeconds">Validated lead over server receipt time.</param>
    /// <returns>The completed regression.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task RefreshedAnchor_ImmediateUndoSurvivesLostReceiptAndRestart(int leadSeconds)
    {
        var account = new AccountIdentity("test", "fresh-anchor-owner");
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
        var occurrence = accepted.Occurrences.Single().Occurrence.Id;
        var initial = await AnchorAt(account, now);
        var completion = new QuestCommand(Guid.NewGuid(), "complete", occurrence, RecordedTime: new(initial.Id, initial.BootId, 1, leadSeconds * 1000, now.AddSeconds(leadSeconds)));
        var completed = await ExecuteAt(account, completion, now);
        Assert.Equal(10, completed.OverallXp);
        Assert.True(completed.Occurrences.Single().CanUndo);

        // Production drain reads current state and replaces the anchor after acknowledging completion.
        await using (var db = Context())
            Assert.Equal(10, (await new SqlQuestStore(db).Read(account, "UTC", now.AddMilliseconds(100), default)).OverallXp);
        var refreshed = await AnchorAt(account, now.AddMilliseconds(200));
        Assert.Equal(now.AddMilliseconds(200), refreshed.ServerUtc);
        Assert.Equal(leadSeconds == 0 ? refreshed.ServerUtc : now.AddSeconds(leadSeconds), refreshed.RecordedTimeFloor);
        var undo = new QuestCommand(Guid.NewGuid(), "undo", occurrence, CompletionId: completion.OperationId, RecordedTime: new(refreshed.Id, refreshed.BootId, 1, 300, refreshed.DeviceUtc.AddMilliseconds(300)));
        var next = new QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", RecordedTime: new(refreshed.Id, refreshed.BootId, 2, 400, refreshed.DeviceUtc.AddMilliseconds(400)));
        var persistedQueue = JsonSerializer.Serialize(new[] { undo, next });
        var receipt = await ExecuteAt(account, undo, now.AddMilliseconds(500));
        Assert.Equal(0, receipt.OverallXp);
        Assert.Equal(QuestStatus.Active, receipt.Occurrences.Single().Status);
        Assert.DoesNotContain(receipt.Ledger, e => e.EventId == completion.OperationId);

        // The response was lost. Restart restores the exact queued payload, then replays before draining the next command.
        var restored = JsonSerializer.Deserialize<QuestCommand[]>(persistedQueue)!;
        var replay = await ExecuteAt(account, restored[0], now.AddSeconds(10));
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(replay));
        Assert.Equal(persistedQueue, JsonSerializer.Serialize(restored));
        var drained = await ExecuteAt(account, restored[1], now.AddSeconds(10));
        Assert.Equal(0, drained.OverallXp);
        Assert.Equal(2, drained.Occurrences.Length);
        await using var final = Context();
        Assert.Equal(1, await final.Completions.CountAsync());
        Assert.Equal(1, await final.Undos.CountAsync());
        Assert.Equal(leadSeconds == 0 ? now.AddMilliseconds(500) : now.AddSeconds(leadSeconds), (await final.Undos.SingleAsync()).RecordedAt);
        Assert.Equal(4, await final.Operations.CountAsync());
        Assert.Equal(0, (await new SqlQuestStore(final).Read(account, "UTC", now.AddSeconds(11), default)).OverallXp);
    }

    /// <summary>Acceptance has the same ordering requirement as completion; refreshed proofs cannot precede accepted terms.</summary>
    /// <param name="leadSeconds">Accepted lead over physical receipt time.</param>
    /// <returns>The completed regression.</returns>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task RefreshedAnchor_CompletionDoesNotPrecedeFutureAcceptance(int leadSeconds)
    {
        var account = new AccountIdentity("test", "acceptance-clock-owner");
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var original = await AnchorAt(account, now);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", RecordedTime: new(original.Id, original.BootId, 1, leadSeconds * 1000, now.AddSeconds(leadSeconds))), now);
        var anchor = await AnchorAt(account, now.AddMilliseconds(200));
        var completed = await ExecuteAt(account, new(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id, RecordedTime: new(anchor.Id, anchor.BootId, 1, 300, anchor.DeviceUtc.AddMilliseconds(300))), now.AddMilliseconds(500));
        Assert.Equal(10, completed.OverallXp);
        Assert.Equal(now.AddSeconds(leadSeconds), completed.Occurrences.Single().Completion!.RecordedAt);
    }

    /// <summary>Repeated synchronization and proof-free online actions use a floor, not an ever-increasing clock offset.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task RepeatedAnchors_DoNotAccumulateLeadOrLoseOnlineCommandOrdering()
    {
        var account = new AccountIdentity("test", "refresh-clock-owner");
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
        var occurrence = accepted.Occurrences.Single().Occurrence.Id;
        var original = await AnchorAt(account, now);
        var completionId = Guid.NewGuid();
        await ExecuteAt(account, new(completionId, "complete", occurrence, RecordedTime: new(original.Id, original.BootId, 1, 5000, now.AddSeconds(5))), now);
        Assert.Equal(0, (await ExecuteAt(account, new(Guid.NewGuid(), "undo", occurrence, CompletionId: completionId), now.AddMilliseconds(100))).OverallXp);
        for (var cycle = 1; cycle <= 8; cycle++)
        {
            var at = now.AddMilliseconds(cycle * 100);
            var anchor = await AnchorAt(account, at);
            Assert.Equal(at, anchor.ServerUtc);
            Assert.Equal(now.AddSeconds(5), anchor.RecordedTimeFloor);
            completionId = Guid.NewGuid();
            var completed = await ExecuteAt(account, new(completionId, "complete", occurrence, RecordedTime: new(anchor.Id, anchor.BootId, 1, 20, anchor.DeviceUtc.AddMilliseconds(20))), at.AddMilliseconds(20));
            Assert.Equal(now.AddSeconds(5), completed.Occurrences.Single().Completion!.RecordedAt);
            Assert.Equal(10, completed.OverallXp);
            Assert.Equal(0, (await ExecuteAt(account, new(Guid.NewGuid(), "undo", occurrence, CompletionId: completionId), at.AddMilliseconds(30))).OverallXp);
        }

        var caughtUp = await AnchorAt(account, now.AddSeconds(6));
        Assert.Equal(caughtUp.ServerUtc, caughtUp.RecordedTimeFloor);
        var finalCompletion = await ExecuteAt(account, new(Guid.NewGuid(), "complete", occurrence, RecordedTime: new(caughtUp.Id, caughtUp.BootId, 1, 100, caughtUp.DeviceUtc.AddMilliseconds(100))), now.AddMilliseconds(6100));
        Assert.Equal(now.AddMilliseconds(6100), finalCompletion.Occurrences.Single().Completion!.RecordedAt);
        Assert.Equal(10, finalCompletion.OverallXp);
    }

    /// <summary>The fixed floor preserves strict 24-hour Undo eligibility for delayed immutable proofs.</summary>
    /// <param name="boundaryOffsetMilliseconds">Offset from the exact end of the Undo window.</param>
    /// <returns>The completed regression.</returns>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task AnchoredFloor_DoesNotExtendUndoWindow(int boundaryOffsetMilliseconds)
    {
        var account = new AccountIdentity("test", "undo-boundary-owner");
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
        var occurrence = accepted.Occurrences.Single().Occurrence.Id;
        var original = await AnchorAt(account, now);
        var completion = Guid.NewGuid();
        await ExecuteAt(account, new(completion, "complete", occurrence, RecordedTime: new(original.Id, original.BootId, 1, 5000, now.AddSeconds(5))), now);
        var anchor = await AnchorAt(account, now.AddMilliseconds(200));
        var at = now.AddSeconds(5).AddHours(24).AddMilliseconds(boundaryOffsetMilliseconds);
        var undo = new QuestCommand(Guid.NewGuid(), "undo", occurrence, CompletionId: completion, RecordedTime: new(anchor.Id, anchor.BootId, 1, (at - anchor.ServerUtc).TotalMilliseconds, at));
        if (boundaryOffsetMilliseconds < 0)
        {
            var result = await ExecuteAt(account, undo, now.AddDays(3));
            Assert.Equal(0, result.OverallXp);
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(await ExecuteAt(account, undo, now.AddDays(4))));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAt(account, undo, now.AddDays(3)));
            await using var db = Context();
            Assert.Empty(await db.Undos.ToListAsync());
            Assert.Equal(2, await db.Operations.CountAsync());
        }
    }

    /// <summary>A new anchor cannot backdate an action behind an already-observed deadline crossing.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task AnchoredFloor_StillRejectsCompletionAtDeadline()
    {
        var account = new AccountIdentity("test", "floor-deadline-owner");
        var now = new DateTimeOffset(2026, 10, 3, 23, 59, 59, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var due = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-10-03"), now);
        var original = await AnchorAt(account, now);
        await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", RecordedTime: new(original.Id, original.BootId, 1, 1000, now.AddSeconds(1))), now);
        var anchor = await AnchorAt(account, now.AddMilliseconds(200));
        Assert.Equal(now.AddSeconds(1), anchor.RecordedTimeFloor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAt(account, new(Guid.NewGuid(), "complete", due.Occurrences.Single().Occurrence.Id, RecordedTime: new(anchor.Id, anchor.BootId, 1, 300, anchor.DeviceUtc.AddMilliseconds(300))), now.AddMilliseconds(500)));
        await using var final = Context();
        Assert.Empty(await final.Completions.ToListAsync());
        Assert.Equal(2, await final.Operations.CountAsync());
    }

    /// <summary>Ordering floors never bypass raw proof, ownership, ordinal or five-second future validation.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task AnchoredFloor_PreservesClockAndOwnershipGuards()
    {
        var account = new AccountIdentity("test", "floor-guard-owner");
        var other = new AccountIdentity("test", "floor-guard-other");
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
        {
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
            await new SqlQuestStore(db).Read(other, "UTC", now, default);
        }

        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
        var occurrence = accepted.Occurrences.Single().Occurrence.Id;
        var original = await AnchorAt(account, now);
        var completion = Guid.NewGuid();
        await ExecuteAt(account, new(completion, "complete", occurrence, RecordedTime: new(original.Id, original.BootId, 1, 5000, now.AddSeconds(5))), now);
        var anchor = await AnchorAt(account, now.AddMilliseconds(200));
        var undo = new QuestCommand(Guid.NewGuid(), "undo", occurrence, CompletionId: completion, RecordedTime: new(anchor.Id, anchor.BootId, 1, 300, anchor.DeviceUtc.AddMilliseconds(300)));
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(other, undo, now.AddMilliseconds(500)));
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(account, undo with { RecordedTime = undo.RecordedTime! with { BootId = Guid.NewGuid() } }, now.AddMilliseconds(500)));
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(account, undo with { RecordedTime = undo.RecordedTime! with { DeviceUtc = now.AddMinutes(3) } }, now.AddMilliseconds(500)));
        await Assert.ThrowsAsync<SyncClockNotReadyException>(() => ExecuteAt(account, undo with { RecordedTime = undo.RecordedTime! with { ElapsedMilliseconds = 5001, DeviceUtc = anchor.DeviceUtc.AddMilliseconds(5001) } }, now.AddMilliseconds(200)));
        await Assert.ThrowsAsync<SyncClockNotReadyException>(() => ExecuteAt(account, undo, now.AddMilliseconds(-1)));
        await Assert.ThrowsAsync<SyncClockNotReadyException>(() => AnchorAt(account, now.AddMilliseconds(-1)));
        Assert.Equal(0, (await ExecuteAt(account, undo, now.AddMilliseconds(500))).OverallXp);
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(account, undo with { OperationId = Guid.NewGuid() }, now.AddSeconds(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(account, undo with { OperationId = Guid.NewGuid(), RecordedTime = undo.RecordedTime! with { Ordinal = 2, ElapsedMilliseconds = 299 } }, now.AddSeconds(1)));
        await using var final = Context();
        Assert.Equal(1, await final.Undos.CountAsync());
        Assert.Equal(3, await final.Operations.CountAsync());
    }

    /// <summary>An additive DACPAC upgrade preserves old anchors and receipts while recovering new floors from committed history.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task ClockFloorUpgrade_PreservesLegacyAnchorsReceiptsAndHistory()
    {
        var account = new AccountIdentity("test", "clock-upgrade-owner");
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
        var original = await AnchorAt(account, now);
        var completion = new QuestCommand(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id, RecordedTime: new(original.Id, original.BootId, 1, 1000, now.AddSeconds(1)));
        var receipt = await ExecuteAt(account, completion, now);
        var legacyRefresh = await AnchorAt(account, now.AddMilliseconds(200));
        var pendingUndo = new QuestCommand(Guid.NewGuid(), "undo", accepted.Occurrences.Single().Occurrence.Id, CompletionId: completion.OperationId, RecordedTime: new(legacyRefresh.Id, legacyRefresh.BootId, 1, 300, legacyRefresh.DeviceUtc.AddMilliseconds(300)));
        var persistedUndo = JsonSerializer.Serialize(pendingUndo);
        await using (var db = Context())
        {
            Assert.StartsWith("PocketQuests_Tests_", db.Database.GetDbConnection().Database, StringComparison.Ordinal);

            // Recreate the immediately preceding schema using only this disposable database.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Accounts DROP COLUMN LastRecordedAt; ALTER TABLE dbo.SyncAnchors DROP COLUMN RecordedTimeFloor;");
            await DatabaseSchema.DeployAsync(db.Database);
            await DatabaseSchema.DeployAsync(db.Database);
            Assert.Null((await db.Accounts.SingleAsync()).LastRecordedAt);
            Assert.All(await db.SyncAnchors.ToListAsync(), anchor => Assert.Null(anchor.RecordedTimeFloor));
        }

        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(await ExecuteAt(account, completion, now.AddMilliseconds(100))));
        var restoredUndo = JsonSerializer.Deserialize<QuestCommand>(persistedUndo)!;
        var undone = await ExecuteAt(account, restoredUndo, now.AddMilliseconds(500));
        Assert.Equal(0, undone.OverallXp);
        Assert.Equal(persistedUndo, JsonSerializer.Serialize(restoredUndo));
        Assert.Equal(JsonSerializer.Serialize(undone), JsonSerializer.Serialize(await ExecuteAt(account, restoredUndo, now.AddSeconds(10))));
        var refreshed = await AnchorAt(account, now.AddMilliseconds(700));
        Assert.Equal(now.AddSeconds(1), refreshed.RecordedTimeFloor);

        // An old anchor keeps its original interpretation; it is not rewritten by newer state.
        var next = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", RecordedTime: new(original.Id, original.BootId, 2, 2000, now.AddSeconds(2))), now.AddDays(1));
        Assert.Contains(next.Occurrences, o => o.Occurrence.AcceptedAt == now.AddSeconds(2));
    }
}
