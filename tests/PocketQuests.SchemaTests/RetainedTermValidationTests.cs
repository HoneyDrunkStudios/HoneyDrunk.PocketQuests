using Microsoft.EntityFrameworkCore;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Retained noncurrent terms fail closed on the actual receipt and offline replay paths.</summary>
/// <param name="fixture">An isolated disposable SQL database.</param>
public sealed class RetainedTermValidationTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Current terms remain readable while unsupported superseded terms reject history replay without writes.</summary>
    /// <param name="assignment">Controlled corruption of one retained field, using only fixed test data.</param>
    /// <returns>Completion after rejection, unchanged proof/state, restoration and valid year-late replay.</returns>
    [Theory]
    [InlineData("RulesetVersion='unsupported'")]
    [InlineData("DisplaySnapshotVersion=99")]
    [InlineData("BaseXp=999")]
    public async Task SupersededTermsRejectReceiptAndAnchorReplayWithoutConsumption(string assignment)
    {
        var owner = new AccountIdentity("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        var workflow = fixture.Commands();
        await workflow.Initialize(owner, "Etc/UTC", Start);
        var quest = new Quest(Guid.NewGuid().ToString("D"), "Original", "Original work", "c01", Rank.F, Effort.Small, [], [], true);
        var save = new QuestCommand(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0);
        var original = await workflow.Execute(owner, save, Start);
        var occurrence = Guid.NewGuid();
        await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.Accept, occurrence, QuestId: quest.Id), Start);
        var boot = Guid.NewGuid();
        var anchor = await workflow.CreateAnchor(owner, Guid.NewGuid(), boot, Start, Start);
        await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest with { Title = "Current", Effort = Effort.Large }, ExpectedRevision: 1), Start.AddHours(1));
        await using var evidence = fixture.Context();
        var account = await evidence.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        var old = await evidence.QuestDefinitionRevision.SingleAsync(row => row.AccountId == account.Id && row.Revision == 1);
        var currentOccurrence = await evidence.QuestOccurrence.SingleAsync(row => row.Id == occurrence);
        Assert.NotEqual(old.Id, currentOccurrence.QuestDefinitionRevisionId);
        var retainedAnchor = await evidence.SyncAnchor.SingleAsync(row => row.Id == anchor.Id);
        var historyCount = await evidence.QuestCommandHistory.CountAsync(row => row.AccountId == account.Id);
        var ledgerCount = await evidence.XpLedgerEntry.CountAsync(row => row.AccountId == account.Id);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence, RecordedTime: new RecordedActionTime(anchor.Id, boot, 1, 60000.125, Start.AddMilliseconds(60000.125)));

        // The fixture accepts fixed test SQL only. Keep all CHECK/FK constraints enabled:
        // supported-rule validation must reject legal but uninterpretable retained versions.
        await fixture.Execute($"UPDATE pocketquests.QuestDefinitionRevision SET {assignment} WHERE Id='{old.Id:D}';");
        try
        {
            Assert.Equal("Current", (await workflow.Read(owner, Start.AddHours(1))).Definitions.Single().Quest.Title);
            await Assert.ThrowsAsync<NotSupportedException>(() => workflow.Execute(owner, save, Start.AddYears(1)));
            await Assert.ThrowsAsync<NotSupportedException>(() => workflow.Execute(owner, complete, Start.AddYears(1)));
            Assert.Equal(account.RowVersion, (await evidence.Account.SingleAsync(row => row.Id == account.Id)).RowVersion);
            Assert.Equal(retainedAnchor.RowVersion, (await evidence.SyncAnchor.SingleAsync(row => row.Id == anchor.Id)).RowVersion);
            Assert.Equal(historyCount, await evidence.QuestCommandHistory.CountAsync(row => row.AccountId == account.Id));
            Assert.Equal(ledgerCount, await evidence.XpLedgerEntry.CountAsync(row => row.AccountId == account.Id));
            Assert.False(await evidence.CommandReceipt.AnyAsync(row => row.Id == complete.OperationId));
            Assert.False(await evidence.QuestCompletion.AnyAsync(row => row.Id == complete.OperationId));
            Assert.False(await evidence.QuestOccurrenceEvent.AnyAsync(row => row.Id == complete.OperationId));
        }
        finally
        {
            await fixture.Execute($"UPDATE pocketquests.QuestDefinitionRevision SET RulesetVersion='1.0',DisplaySnapshotVersion=2,BaseXp=10 WHERE Id='{old.Id:D}';");
        }

        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await workflow.Execute(owner, save, Start.AddYears(1))));
        var accepted = await workflow.Execute(owner, complete, Start.AddYears(1));
        Assert.Equal(10, accepted.OverallXp);
        Assert.Equal("Original", accepted.Occurrences.Single().Occurrence.Quest.Title);
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(await workflow.Execute(owner, complete, Start.AddYears(4))));
    }
}
