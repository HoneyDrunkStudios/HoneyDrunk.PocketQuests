using PocketQuests.Domain.Commands;

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
        var replay = await Command(client, operation);
        Assert.Equal(rewards.Length, replay.Ledger.Count(e => e.EventId == operation.OperationId));

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
    }
}
