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

    private async Task<SyncAnchor> AnchorAt(AccountIdentity identity, DateTimeOffset at)
    {
        await using var db = Context();
        return await new SqlQuestStore(db).CreateAnchor(identity, Guid.NewGuid(), Guid.NewGuid(), at, at, default);
    }
}
