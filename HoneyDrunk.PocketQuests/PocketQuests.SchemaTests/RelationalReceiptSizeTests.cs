using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Legal custom pools have no finite recipient cap, so receipt size must be independent of feedback and account size.</summary>
/// <param name="fixture">Disposable real SQL fixture.</param>
public sealed class RelationalReceiptSizeTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>Maximum-length escaped labels can exceed the former outcome bound; typed history still reconstructs them from a fixed-size receipt.</summary>
    /// <returns>Completion after oversized legal feedback, later edits/Undo and exact old response replay.</returns>
    [Fact]
    public async Task LargeLegalFeedbackNeverInflatesOrTruncatesCompactReceipts()
    {
        var at = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var owner = new AccountIdentity("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        var store = fixture.Commands();
        await store.Initialize(owner, "Etc/UTC", at);
        var skills = Enumerable.Range(0, 64).Select(_ => Guid.NewGuid().ToString("D")).ToArray();
        for (var index = 0; index < skills.Length; index++)
            await store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: skills[index], SkillName: new string('\u754c', 76) + index.ToString("D4", CultureInfo.InvariantCulture), ExpectedRevision: 0), at);
        var shares = skills.Select((id, index) => new Share(id, index == 63 ? 172 : 156)).ToImmutableArray();
        var quest = new Quest(Guid.NewGuid().ToString("D"), new string('T', 120), new string('C', 2000), "c01", Rank.F, Effort.Large, [new("a01", 10000)], shares, true, new string('D', 2000));
        await store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), at);
        var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: quest.Id);
        await store.Execute(owner, accept, at);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        var original = await store.Execute(owner, complete, at);
        Assert.Equal(800, original.OverallXp);
        var feedbackBytes = Encoding.Unicode.GetByteCount(JsonSerializer.Serialize(original.CompletionOutcome));
        Assert.True(feedbackBytes > 65536, $"The legal feedback stress case must cross the old bound; actual UTF-16 bytes: {feedbackBytes}.");
        await store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: skills[0], SkillName: "Later renamed skill", ExpectedRevision: 1), at.AddMinutes(1));
        await store.Execute(owner, new(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId), at.AddMinutes(2));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await store.Execute(owner, complete, at.AddYears(2))));
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var receipts = await db.Set<CommandReceiptEntity>().Where(r => r.AccountId == account.Id).ToListAsync();
        Assert.All(receipts, receipt =>
        {
            Assert.Equal(2, receipt.OutcomeVersion);
            Assert.InRange(Encoding.Unicode.GetByteCount(receipt.OutcomeJson), 1, 256);
            using var outcome = JsonDocument.Parse(receipt.OutcomeJson);
            Assert.Equal(2, outcome.RootElement.EnumerateObject().Count());
            Assert.Equal(JsonValueKind.Null, outcome.RootElement.GetProperty("CompletionOutcome").ValueKind);
        });
        var directory = Environment.GetEnvironmentVariable("POCKETQUESTS_SCHEMA_EVIDENCE");
        if (directory is not null)
            await File.WriteAllTextAsync(Path.Combine(directory, "receipt-compactness.json"), JsonSerializer.Serialize(new { skills = skills.Length, feedbackBytes, maximumReceiptBytes = receipts.Max(r => Encoding.Unicode.GetByteCount(r.OutcomeJson)), exactOldResponseReplayed = true }));
    }
}
