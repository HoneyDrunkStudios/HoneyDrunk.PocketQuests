using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.Repositories;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Synchronization;

namespace PocketQuests.Tests.Integration;

/// <summary>Late synchronization uses verified anchor time and immutable cached terms, with real SQL receipts.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Late receipt honors pre-cutoff recording and repeated replay grants only once.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task ValidAnchoredOfflineCompletionWinsOverProvisionalMiss()
    {
        var account = new AccountIdentity("test", "offline-owner");
        var now = new DateTimeOffset(2026, 9, 28, 23, 58, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28"), now);
        var anchor = await AnchorAt(account, now);
        var proof = new RecordedActionTime(anchor.Id, anchor.BootId, 1, 60000, anchor.DeviceUtc.AddMinutes(1));
        var command = new QuestCommand(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id, RecordedTime: proof);
        await using (var db = Context())
        {
            var missed = await new SqlQuestStore(db).Read(account, "UTC", now.AddDays(10), default);
            Assert.Equal(QuestStatus.Missed, missed.Occurrences.Single().Status);
        }

        var complete = await ExecuteAt(account, command, now.AddDays(10));
        Assert.Equal(10, complete.OverallXp);
        Assert.Equal(now.AddMinutes(1), complete.Occurrences.Single().Completion!.RecordedAt);
        Assert.Equal(10, (await ExecuteAt(account, command, now.AddDays(11))).OverallXp);
        await using var final = Context();
        Assert.Equal(1, await final.Completions.CountAsync());
    }

    /// <summary>Clock jumps, reset processes, cross-account anchors and exact-cutoff timestamps fail safely.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task OfflineClockAndOwnershipConflictsDoNotWriteAwardsOrReceipts()
    {
        var account = new AccountIdentity("test", "offline-owner");
        var other = new AccountIdentity("test", "other-owner");
        var now = new DateTimeOffset(2026, 9, 28, 23, 58, 0, TimeSpan.Zero);
        await using (var db = Context())
        {
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
            await new SqlQuestStore(db).Read(other, "UTC", now, default);
        }

        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28"), now);
        var anchor = await AnchorAt(account, now);
        var id = accepted.Occurrences.Single().Occurrence.Id;
        var valid = new RecordedActionTime(anchor.Id, anchor.BootId, 1, 60000, anchor.DeviceUtc.AddMinutes(1));
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(account, new(Guid.NewGuid(), "complete", id, RecordedTime: valid with { DeviceUtc = now.AddHours(4) }), now.AddDays(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(account, new(Guid.NewGuid(), "complete", id, RecordedTime: valid with { BootId = Guid.NewGuid() }), now.AddDays(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(other, new(Guid.NewGuid(), "complete", id, RecordedTime: valid), now.AddDays(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAt(account, new(Guid.NewGuid(), "complete", id, RecordedTime: valid with { ElapsedMilliseconds = 120000, DeviceUtc = now.AddMinutes(2) }), now.AddDays(1)));
        await using var final = Context();
        Assert.Equal(0, await final.Completions.CountAsync());
        Assert.Equal(1, await final.Operations.CountAsync());
    }

    /// <summary>Changes made after recording cannot alter the completed cached revision's reward or text.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task OfflineCompletionKeepsAnchoredDefinitionRevisionAfterOnlineEdit()
    {
        var account = new AccountIdentity("test", "offline-owner");
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var quest = new Quest(Guid.NewGuid().ToString(), "Recorded original", "Original criterion", "c07", Rank.F, Effort.Small, [], [], true);
        await ExecuteAt(account, new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0), now);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: quest.Id), now);
        var anchor = await AnchorAt(account, now);
        await ExecuteAt(account, new(Guid.NewGuid(), "save-definition", Definition: quest with { Title = "Later revision", Effort = Effort.Medium }, ExpectedRevision: 1), now.AddHours(2));
        var complete = await ExecuteAt(account, new(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id, RecordedTime: new(anchor.Id, anchor.BootId, 1, 60000, now.AddMinutes(1))), now.AddHours(3));
        Assert.Equal(10, complete.OverallXp);
        Assert.Equal("Recorded original", complete.Occurrences.Single().Completion!.Snapshot!.Title);
        Assert.Equal("Later revision", complete.Definitions.Single().Quest.Title);
    }

    /// <summary>Offline creation, acceptance, completion and Undo use stable IDs across process restart.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task OfflineCreateAcceptCompleteUndoConvergesWithStableIds()
    {
        var account = new AccountIdentity("test", "offline-owner");
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var anchor = await AnchorAt(account, now);
        RecordedActionTime Proof(int i) => new(anchor.Id, anchor.BootId, i, i * 1000, now.AddSeconds(i));
        var quest = new Quest(Guid.NewGuid().ToString(), "Offline quest", "Done", "c07", Rank.F, Effort.Small, [], [], true);
        await ExecuteAt(account, new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0, RecordedTime: Proof(1)), now.AddDays(3));
        var occurrenceId = Guid.NewGuid();
        await ExecuteAt(account, new(Guid.NewGuid(), "accept", occurrenceId, QuestId: quest.Id, DueDate: "2026-09-28", RecordedTime: Proof(2)), now.AddDays(3));
        var completionId = Guid.NewGuid();
        Assert.Equal(10, (await ExecuteAt(account, new(completionId, "complete", occurrenceId, RecordedTime: Proof(3)), now.AddDays(3))).OverallXp);
        Assert.Equal(0, (await ExecuteAt(account, new(Guid.NewGuid(), "undo", occurrenceId, CompletionId: completionId, RecordedTime: Proof(4)), now.AddDays(3))).OverallXp);
    }

    /// <summary>Accepted clock lead is visible in durable receipts and immediate live reads.</summary>
    /// <param name="leadSeconds">Validated lead over receipt time, including both tolerance boundaries.</param>
    /// <returns>The completed regression.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task AnchoredCompletionClockLead_PreservesReceiptLevelUpAndImmediateUndo(int leadSeconds)
    {
        var account = new AccountIdentity("test", "clock-lead-owner");
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        for (var count = 0; count < 9; count++)
        {
            var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
            var id = accepted.Occurrences.Single(o => o.Status == QuestStatus.Active).Occurrence.Id;
            await ExecuteAt(account, new(Guid.NewGuid(), "complete", id), now);
        }

        var next = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"), now);
        var occurrence = next.Occurrences.Single(o => o.Status == QuestStatus.Active).Occurrence.Id;
        var anchor = await AnchorAt(account, now);
        var recorded = now.AddSeconds(leadSeconds);
        var proof = new RecordedActionTime(anchor.Id, anchor.BootId, 1, leadSeconds * 1000, recorded);
        var command = new QuestCommand(Guid.NewGuid(), "complete", occurrence, RecordedTime: proof);
        var receipt = await ExecuteAt(account, command, now);
        Assert.Equal(100, receipt.OverallXp);
        Assert.Equal(2, receipt.OverallLevel);
        var completed = receipt.Occurrences.Single(o => o.Occurrence.Id == occurrence);
        Assert.Equal(QuestStatus.Completed, completed.Status);
        Assert.Equal(recorded, completed.Completion!.RecordedAt);
        Assert.Equal(command.OperationId, receipt.CompletionOutcome!.CompletionId);
        Assert.Contains(receipt.CompletionOutcome.LevelUps, l => l.Track == "Overall" && l.From == 1 && l.To == 2);
        Assert.Equal(10, Assert.Single(receipt.Ledger, e => e.EventId == command.OperationId && e.Track == "Overall").Amount);
        await using (var db = Context())
        {
            var current = await new SqlQuestStore(db).Read(account, "UTC", now, default);
            Assert.Equal(100, current.OverallXp);
            Assert.Equal(command.OperationId, current.Occurrences.Single(o => o.Occurrence.Id == occurrence).Completion!.Id);
            Assert.Null(current.CompletionOutcome);
        }

        var noOp = await ExecuteAt(account, new(Guid.NewGuid(), "complete", occurrence), now);
        Assert.Equal(100, noOp.OverallXp);
        Assert.Null(noOp.CompletionOutcome);
        var replay = await ExecuteAt(account, command, now.AddSeconds(6));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(receipt), System.Text.Json.JsonSerializer.Serialize(replay));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAt(account, command with { Action = "undo" }, now.AddSeconds(6)));

        var undoAt = recorded.AddMilliseconds(100);
        var undoProof = proof with { Ordinal = 2, ElapsedMilliseconds = (leadSeconds * 1000) + 100, DeviceUtc = undoAt };
        var undone = await ExecuteAt(account, new(Guid.NewGuid(), "undo", occurrence, CompletionId: command.OperationId, RecordedTime: undoProof), now.AddSeconds(1));
        Assert.Equal(90, undone.OverallXp);
        Assert.Equal(1, undone.OverallLevel);
        Assert.DoesNotContain(undone.Ledger, e => e.EventId == command.OperationId);
        await using var final = Context();
        Assert.Equal(90, (await new SqlQuestStore(final).Read(account, "UTC", now.AddSeconds(1), default)).OverallXp);
        Assert.Equal(10, await final.Completions.CountAsync());
        Assert.Equal(1, await final.Undos.CountAsync());
    }

    /// <summary>The projection watermark does not permit a claimed completion at or beyond its deadline.</summary>
    /// <param name="leadMilliseconds">Clock lead reaching or crossing the deadline.</param>
    /// <returns>The completed regression.</returns>
    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    public async Task AnchoredFutureCompletion_StillRejectsDeadlineAndExcessiveLead(int leadMilliseconds)
    {
        var account = new AccountIdentity("test", "deadline-owner");
        var now = new DateTimeOffset(2026, 10, 3, 23, 59, 59, TimeSpan.Zero);
        await using (var db = Context())
            await new SqlQuestStore(db).Read(account, "UTC", now, default);
        var accepted = await ExecuteAt(account, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07", DueDate: "2026-10-03"), now);
        var anchor = await AnchorAt(account, now);
        var proof = new RecordedActionTime(anchor.Id, anchor.BootId, 1, leadMilliseconds, now.AddMilliseconds(leadMilliseconds));
        var command = new QuestCommand(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id, RecordedTime: proof);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAt(account, command, now));
        var excessive = command with { RecordedTime = proof with { ElapsedMilliseconds = 5001, DeviceUtc = now.AddMilliseconds(5001) } };
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAt(account, excessive, now));
        await using var final = Context();
        Assert.Equal(0, await final.Completions.CountAsync());
        Assert.Equal(1, await final.Operations.CountAsync());
        Assert.Equal(0, (await new SqlQuestStore(final).Read(account, "UTC", now, default)).OverallXp);
    }

    private async Task<SyncAnchor> AnchorAt(AccountIdentity identity, DateTimeOffset at)
    {
        await using var db = Context();
        return await new SqlQuestStore(db).CreateAnchor(identity, Guid.NewGuid(), Guid.NewGuid(), at, at, default);
    }
}
