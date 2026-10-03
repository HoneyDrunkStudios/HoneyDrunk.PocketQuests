using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Quests.Occurrences;

namespace PocketQuests.Tests.Integration;

/// <summary>Verifies the existing API ledger used by native completion feedback.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Confirms completion-specific pools, replay and Undo against isolated SQL.</summary>
    /// <returns>A task completing after persisted reward assertions.</returns>
    [Fact]
    public async Task CompletionLedger_ConfirmedReplayAndUndo_PreservesMatchingRewards()
    {
        using var host = new Host(Connection);
        using var client = host.Client("ledger-user");
        await Setup(client);
        var accepted = await Command(client, new(Guid.NewGuid(), QuestActions.Accept, QuestId: "PQ-CAT-Q01"));
        var first = accepted.Occurrences.Single().Occurrence.Id;
        var operation = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, first);
        var completed = await Command(client, operation);
        var rewards = completed.Ledger.Where(e => e.EventId == operation.OperationId).ToArray();
        Assert.All(rewards, e => Assert.Equal(first, e.OccurrenceId));
        Assert.Equal(10, Assert.Single(rewards, e => e.Track == "Overall").Amount);
        Assert.Equal(10, Assert.Single(rewards, e => e.Track == "Category").Amount);
        Assert.Equal(10, rewards.Where(e => e.Track == "Attribute").Sum(e => e.Amount));
        Assert.Equal(10, Assert.Single(rewards, e => e.Track == "Skill").Amount);
        Assert.Equal(2, completed.Categories.Single(c => c.Id == "c01").Level);
        var outcome = Assert.IsType<PocketQuests.Domain.Projections.CompletionOutcome>(completed.CompletionOutcome);
        Assert.Equal(operation.OperationId, outcome.CompletionId);
        Assert.Equal(first, outcome.OccurrenceId);
        Assert.Contains(outcome.LevelUps, l => l.Track == "Category" && l.TrackId == "c01" && l.From == 1 && l.To == 2);
        Assert.Contains(outcome.LevelUps, l => l.Track == "Skill" && l.TrackId == "s01" && l.From == 1 && l.To == 2);
        Assert.DoesNotContain(outcome.LevelUps, l => l.Track == "Overall");
        Assert.Null((await Read(client)).CompletionOutcome);
        var replay = await Command(client, operation);
        Assert.Equal(rewards.Length, replay.Ledger.Count(e => e.EventId == operation.OperationId));
        Assert.Equal(outcome.LevelUps.ToArray(), replay.CompletionOutcome!.LevelUps.ToArray());
        var noOp = await Command(client, new(Guid.NewGuid(), QuestActions.Complete, first));
        Assert.Null(noOp.CompletionOutcome);

        var other = await Command(client, new(Guid.NewGuid(), QuestActions.Accept, QuestId: "PQ-CAT-Q07"));
        var second = other.Occurrences.Single(o => o.Occurrence.Id != first).Occurrence.Id;
        var secondOperation = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, second);
        await Command(client, secondOperation);
        var undone = await Command(client, new(Guid.NewGuid(), QuestActions.Undo, first, CompletionId: operation.OperationId));
        Assert.DoesNotContain(undone.Ledger, e => e.EventId == operation.OperationId);
        Assert.Equal(10, undone.OverallXp);
        Assert.Equal(2, undone.Ledger.Count(e => e.EventId == secondOperation.OperationId));
        Assert.Equal(1, undone.Categories.Single(c => c.Id == "c01").Level);
        Assert.Equal(10, (await Read(client)).OverallXp);
        Assert.Null(undone.CompletionOutcome);

        // A receipt stays immutable after Undo. Clients must reconcile it with current state.
        var historical = await Command(client, operation);
        Assert.Equal(outcome.LevelUps.ToArray(), historical.CompletionOutcome!.LevelUps.ToArray());
        var recompleted = await Command(client, new(Guid.NewGuid(), QuestActions.Complete, first));
        var staleUndo = await Command(client, new(Guid.NewGuid(), QuestActions.Undo, first, CompletionId: operation.OperationId));
        Assert.Equal(
            recompleted.Occurrences.Single(o => o.Occurrence.Id == first).Completion!.Id,
            staleUndo.Occurrences.Single(o => o.Occurrence.Id == first).Completion!.Id);
        Assert.Equal(20, (await Read(client)).OverallXp);
    }

    /// <summary>Overall level notification is based on locked server state and survives API restart.</summary>
    /// <returns>A task completing after immutable receipt assertions.</returns>
    [Fact]
    public async Task CompletionOutcome_OverallThresholdAndRestart_PreservesTransactionLevels()
    {
        QuestCommand last = null!;
        using (var host = new Host(Connection))
        using (var client = host.Client("threshold-user"))
        {
            await Setup(client);
            for (var count = 0; count < 10; count++)
            {
                var accepted = await Command(client, new(Guid.NewGuid(), QuestActions.Accept, QuestId: "PQ-CAT-Q07"));
                var occurrence = accepted.Occurrences.Single(o => o.Status == QuestStatus.Active).Occurrence.Id;
                last = new(Guid.NewGuid(), QuestActions.Complete, occurrence);
                var result = await Command(client, last);
                var overall = result.CompletionOutcome!.LevelUps.Where(l => l.Track == "Overall");
                if (count < 9)
                {
                    Assert.Empty(overall);
                }
                else
                {
                    var level = Assert.Single(overall);
                    Assert.Equal(1, level.From);
                    Assert.Equal(2, level.To);
                }
            }
        }

        using (var restarted = new Host(Connection))
        using (var client = restarted.Client("threshold-user"))
        {
            var receipt = await Command(client, last);
            Assert.Equal(100, receipt.OverallXp);
            var level = Assert.Single(receipt.CompletionOutcome!.LevelUps, l => l.Track == "Overall");
            Assert.Equal(1, level.From);
            Assert.Equal(2, level.To);
            Assert.Equal(10, (await Read(client)).Ledger.Count(e => e.Track == "Overall"));
        }
    }
}
