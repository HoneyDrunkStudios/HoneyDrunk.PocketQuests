using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Services.Quests;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Real SQL parity across profile, editing, recurrence and penalty command histories.</summary>
/// <param name="fixture">Independently deployed synthetic database.</param>
public sealed class RelationalHistoryTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private IQuestService Store => fixture.Commands();

    /// <summary>Undo clears a revoked selection durably; re-earning never silently equips it during receipt replay.</summary>
    /// <returns>Completion after comparing current and original-response profile selection.</returns>
    [Fact]
    public async Task RevokedRewardStaysUnequippedAfterReearningAndReplay()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var questId = PocketQuests.Domain.Catalogs.Catalog.Quests.First(q => q.CategoryId == "c10").Id;
        QuestCommand? last = null;
        for (var index = 0; index < 3; index++)
        {
            var occurrence = Guid.NewGuid();
            await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Accept, occurrence, QuestId: questId), Start);
            last = new(Guid.NewGuid(), QuestActions.Complete, occurrence);
            await Store.Execute(owner, last, Start);
        }

        var select = new QuestCommand(Guid.NewGuid(), QuestActions.SelectBadge, RewardId: "PQ-CAT-B01");
        var selected = await Store.Execute(owner, select, Start);
        Assert.Equal("PQ-CAT-B01", selected.Profile.BadgeId);
        var undone = await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Undo, last!.OccurrenceId, CompletionId: last.OperationId), Start.AddMinutes(1));
        Assert.Null(undone.Profile.BadgeId);
        var again = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, last.OccurrenceId);
        var earnedAgain = await Store.Execute(owner, again, Start.AddMinutes(2));
        Assert.True(earnedAgain.Entitlements.Single(e => e.Id == "PQ-CAT-B01").Earned);
        Assert.Null(earnedAgain.Profile.BadgeId);
        Equal(earnedAgain, await Store.Execute(owner, again, Start.AddYears(1)));
        Equal(selected, await Store.Execute(owner, select, Start.AddYears(1)));
    }

    /// <summary>Every existing command action retains its original domain result after later edits and archival.</summary>
    /// <returns>Completion after current-state and every-receipt replay comparisons.</returns>
    [Fact]
    public async Task AllCommandLanesRoundTripAndOldResponsesSurviveMutableProfileAndTerms()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var domain = new QuestAggregate("Etc/UTC");
        var history = new List<(QuestCommand command, QuestState result)>();
        var at = Start;
        var skillId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var quest = new Quest(Guid.NewGuid().ToString("D"), "Typed custom terms", "A specific achieved outcome", "c01", Rank.F, Effort.Medium, [new("a02", 10000), new("a01", 0)], [new(skillId, 5000), new("s01", 5000)], true, "Historical description");
        var parent = quest with { Id = Guid.NewGuid().ToString("D"), Title = "Parent", Effort = Effort.Large, Skills = [] };
        await Run(new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: skillId, SkillName: "  Focus 日  ", ExpectedRevision: 0));
        await Run(new(Guid.NewGuid(), QuestActions.AssessSkill, SkillId: skillId, Experience: Experience.Expert));
        await Run(new(Guid.NewGuid(), QuestActions.AssessSkill, SkillId: "s01", Experience: Experience.Practiced));
        await Run(new(Guid.NewGuid(), QuestActions.Interests, Interests: ["c03", "c01"]));
        await Run(new(Guid.NewGuid(), QuestActions.FinishOnboarding));
        await Run(new(Guid.NewGuid(), QuestActions.ExpiryWarnings, ExpiryWarnings: true));
        await Run(new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0));
        var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: quest.Id, DueDate: "2026-01-02", PlannedTime: "13:30");
        await Run(accept);
        var completion = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        await Run(completion);
        await Run(new(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: completion.OperationId));
        quest = quest with { Title = "Edited custom terms", Attributes = [new("a01", 10000)], Description = "Later description" };
        await Run(new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 1));
        await Run(new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: parent, ExpectedRevision: 0));
        var parentAccept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: parent.Id);
        await Run(parentAccept);
        await Run(new(Guid.NewGuid(), QuestActions.Link, accept.OccurrenceId, ParentId: parentAccept.OccurrenceId));
        await Run(new(Guid.NewGuid(), QuestActions.Plan, accept.OccurrenceId, DueDate: "2026-01-03", PlannedTime: "08:15"));
        await Run(new(Guid.NewGuid(), QuestActions.Pause, CategoryId: "c01"));
        await Run(new(Guid.NewGuid(), QuestActions.Pause, CategoryId: "c02"));
        await Run(new(Guid.NewGuid(), QuestActions.Pause, CategoryId: "c01"));
        await Run(new(Guid.NewGuid(), QuestActions.Pause));
        await Run(new(Guid.NewGuid(), QuestActions.Resume, CategoryId: "c01"));
        await Run(new(Guid.NewGuid(), QuestActions.Resume, CategoryId: "c02"));
        await Run(new(Guid.NewGuid(), QuestActions.Resume));
        var seriesId = Guid.NewGuid();
        await Run(new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest.Id, DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: seriesId, Cadence: Cadence.Days, Interval: 1));
        var delivered = domain.Occurrences.Single(o => o.Lifecycle?.SeriesId == seriesId);
        await Run(new(Guid.NewGuid(), QuestActions.StopSeries, SeriesId: seriesId));
        await Run(new(Guid.NewGuid(), QuestActions.ResumeOccurrence, delivered.Id));
        await Run(new(Guid.NewGuid(), QuestActions.Abandon, delivered.Id));
        await Run(new(Guid.NewGuid(), QuestActions.Zone, NewZone: "America/New_York", ExpectedZone: "Etc/UTC", ConfirmZoneChange: true));
        await Run(new(Guid.NewGuid(), QuestActions.SelectBadge));
        await Run(new(Guid.NewGuid(), QuestActions.SelectFrame));
        await Run(new(Guid.NewGuid(), QuestActions.Interests, Interests: []));
        await Run(new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: skillId, SkillName: "Renamed focus", ExpectedRevision: 1));
        await Run(new(Guid.NewGuid(), QuestActions.ArchiveDefinition, QuestId: quest.Id, ExpectedRevision: 2));
        await Run(new(Guid.NewGuid(), QuestActions.ArchiveDefinition, QuestId: parent.Id, ExpectedRevision: 1));
        await Run(new(Guid.NewGuid(), QuestActions.ArchiveSkill, SkillId: skillId, ExpectedRevision: 2));
        foreach (var (command, expected) in history)
            Equal(expected, await Store.Execute(owner, command, Start.AddYears(3)));
        Equal(domain.Project(at), await Store.Read(owner, at));
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        Assert.Equal(history.Count, await db.Set<QuestCommandHistoryEntity>().CountAsync(h => h.AccountId == account.Id));
        Assert.All(await db.Set<CommandReceiptEntity>().Where(r => r.AccountId == account.Id).ToListAsync(), r =>
        {
            Assert.Equal(2, r.OutcomeVersion);
            Assert.InRange(System.Text.Encoding.Unicode.GetByteCount(r.OutcomeJson), 1, 256);
        });
        var custom = await db.Set<CustomSkillEntity>().SingleAsync(s => s.AccountId == account.Id);
        Assert.Equal("Renamed focus", custom.Name);
        Assert.NotNull(custom.ArchivedAt);
        Assert.True(await db.Set<AccountPauseEntity>().AnyAsync(p => p.AccountId == account.Id && p.EndedAt != null));
        Assert.Equal(2, await db.Set<SkillAssessmentEntity>().CountAsync(a => a.AccountId == account.Id));

        async Task Run(QuestCommand command)
        {
            at = at.AddMinutes(1);
            domain.Reconcile(at);
            var before = domain.Project(at);
            domain.Apply(command, at);
            var expected = domain.Project(at);
            if (command.Action == QuestActions.Complete)
                expected = expected with { CompletionOutcome = CompletionOutcome.Between(before, expected, command.OperationId, command.OccurrenceId!.Value) };
            Equal(expected, await Store.Execute(owner, command, at));
            history.Add((command, expected));
        }
    }

    /// <summary>A late completion uses the issued anchor's quest terms despite later edits, and its receipt stays stable after Undo.</summary>
    /// <returns>Completion after verifying old/new immutable revisions and replayed feedback.</returns>
    [Fact]
    public async Task AnchoredCompletionUsesOriginalTermsAfterDefinitionEditAndYearLateDelivery()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var quest = new Quest(Guid.NewGuid().ToString("D"), "Original", "Finish original work", "c01", Rank.F, Effort.Small, [], [], true);
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), Start);
        var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: quest.Id);
        await Store.Execute(owner, accept, Start);
        var boot = Guid.NewGuid();
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), boot, Start, Start);
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest with { Title = "Much larger edit", Effort = Effort.Large }, ExpectedRevision: 1), Start.AddHours(1));
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId, RecordedTime: new RecordedActionTime(anchor.Id, boot, 1, 60000.125, Start.AddMilliseconds(60000.125)));
        var original = await Store.Execute(owner, complete, Start.AddYears(1));
        Assert.Equal(10, original.OverallXp);
        Assert.Equal("Original", original.Occurrences.Single().Occurrence.Quest.Title);
        Assert.Equal("Much larger edit", original.Definitions.Single().Quest.Title);
        var undo = new QuestCommand(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId, RecordedTime: new RecordedActionTime(anchor.Id, boot, 2, 120000.25, Start.AddMilliseconds(120000.25)));
        await Store.Execute(owner, undo, Start.AddYears(1));
        Equal(original, await Store.Execute(owner, complete, Start.AddYears(4)));
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        Assert.Equal(2, await db.Set<QuestDefinitionRevisionEntity>().CountAsync(r => r.AccountId == account.Id));
        Assert.True(await db.Set<QuestOccurrenceRevisionEntity>().CountAsync(r => r.AccountId == account.Id) >= 4);
    }

    /// <summary>Unaccepted recurring offers retain delivery time, explicit penalty consent and chronological Undo-driven losses.</summary>
    /// <returns>Completion after comparing late penalty and offered history with the unchanged domain.</returns>
    [Fact]
    public async Task RecurringPenaltyOfferAcceptanceAndUndoAfterDeadlineMatchDomain()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var domain = new QuestAggregate("Etc/UTC");
        var quest = new Quest(Guid.NewGuid().ToString("D"), "Penalty offer", "Achieve the result", "c01", Rank.F, Effort.Small, [], [], true, PenaltyPercent: 50);
        await Run(new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), Start);
        var series = Guid.NewGuid();
        var createSeries = new QuestCommand(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest.Id, DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: series, Cadence: Cadence.Days, Interval: 1);
        var offered = await Run(createSeries, Start);
        var occurrence = offered.Occurrences.Single().Occurrence;
        Assert.True(occurrence.Lifecycle!.Unaccepted);
        await Run(new(Guid.NewGuid(), QuestActions.AcceptOffer, occurrence.Id, ConfirmPenalty: true, AcceptedLoss: 5, AcceptedQuest: quest), Start.AddMinutes(1));
        var completion = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence.Id);
        await Run(completion, Start.AddMinutes(2));
        var result = await Run(new(Guid.NewGuid(), QuestActions.Undo, occurrence.Id, CompletionId: completion.OperationId), Start.AddHours(13));
        Assert.NotEmpty(result.Penalties);
        Equal(offered, await Store.Execute(owner, createSeries, Start.AddYears(3)));

        async Task<QuestState> Run(QuestCommand command, DateTimeOffset at)
        {
            domain.Reconcile(at);
            var before = domain.Project(at);
            domain.Apply(command, at);
            var expected = domain.Project(at);
            if (command.Action == QuestActions.Complete)
                expected = expected with { CompletionOutcome = CompletionOutcome.Between(before, expected, command.OperationId, command.OccurrenceId!.Value) };
            var actual = await Store.Execute(owner, command, at);
            Equal(expected, actual);
            return actual;
        }
    }

    private static AccountIdentity Identity() => new("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());

    private static void Equal(QuestState expected, QuestState actual) => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
}
