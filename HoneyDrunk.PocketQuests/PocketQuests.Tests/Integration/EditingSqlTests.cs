using Microsoft.EntityFrameworkCore;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Occurrences;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Real SQL and authenticated two-service regressions for definitions and onboarding.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Definition management preserves unfinished commitments and existing eligibility/revision rules.</summary>
    /// <param name="frozen">Whether the accepted commitment is paused before editing.</param>
    /// <returns>The completed regression.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveAndFrozenDefinitions_RemainEditableAndArchivable(bool frozen)
    {
        using var host = new Host(Connection);
        using var client = host.Client("definition-owner");
        await Setup(client);
        var quest = new Quest(Guid.NewGuid().ToString(), "My commitment", "Original result", "c07", Rank.F, Effort.Small, [], [], true);
        await Command(client, new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0));
        var accepted = await Command(client, new(Guid.NewGuid(), "accept", QuestId: quest.Id));
        var occurrence = accepted.Occurrences.Single().Occurrence.Id;
        if (frozen)
            await Command(client, new(Guid.NewGuid(), "pause", CategoryId: "c07"));
        var revised = quest with { Criterion = "Revised clear result", Effort = Effort.Medium };
        var edited = await Command(client, new(Guid.NewGuid(), "save-definition", Definition: revised, ExpectedRevision: 1));
        var view = edited.Occurrences.Single(o => o.Occurrence.Id == occurrence);
        Assert.Equal(frozen ? QuestStatus.Frozen : QuestStatus.Active, view.Status);
        Assert.Equal("Revised clear result", view.Occurrence.Quest.Criterion);
        Assert.Equal(80, view.Occurrence.Quest.BaseXp);
        Assert.Equal(0, edited.OverallXp);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-definition", Definition: revised, ExpectedRevision: 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-definition", Definition: revised with { Rank = Rank.S }, ExpectedRevision: 2))).StatusCode);
        var archived = await Command(client, new(Guid.NewGuid(), "archive-definition", QuestId: quest.Id, ExpectedRevision: 2));
        Assert.True(archived.Definitions.Single().Archived);
        Assert.Equal(view.Status, archived.Occurrences.Single().Status);
        Assert.Equal("Revised clear result", archived.Occurrences.Single().Occurrence.Quest.Criterion);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "accept", QuestId: quest.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-definition", Definition: revised, ExpectedRevision: 3))).StatusCode);
        Assert.Equal(0, (await Read(client)).OverallXp);
    }

    /// <summary>Checks seed replacement, authoritative eligibility and durable isolated definitions.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task CustomDefinitionsAndAssessmentsSurviveRestartWithoutSeedingOtherTracks()
    {
        var quest = new Quest(Guid.NewGuid().ToString(), " Ship my tool ", "A usable tool is delivered", "c04", Rank.A, Effort.Small, [], [new("s07", 10000)], true);
        using (var host = new Host(Connection))
        using (var alice = host.Client("alice"))
        using (var bob = host.Client("bob"))
        {
            await Setup(alice);
            await Setup(bob);
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0))).StatusCode);
            await Command(alice, new(Guid.NewGuid(), "assess-skill", SkillId: "s07", Experience: Experience.Expert));
            await Command(alice, new(Guid.NewGuid(), "interests", Interests: ["c04"]));
            await Command(alice, new(Guid.NewGuid(), "finish-onboarding"));
            var save = new QuestCommand(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0);
            await Command(alice, save);
            Assert.Single((await Command(alice, save)).Definitions);
            Assert.Empty((await Read(bob)).Definitions);
            Assert.Equal(HttpStatusCode.BadRequest, (await bob.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "accept", QuestId: quest.Id))).StatusCode);
        }

        using (var host = new Host(Connection))
        using (var alice = host.Client("alice"))
        {
            var loaded = await Read(alice);
            Assert.Equal("Ship my tool", loaded.Definitions.Single().Quest.Title);
            Assert.True(loaded.Profile.OnboardingComplete);
            Assert.Equal("c04", Assert.Single(loaded.Profile.Interests));
            Assert.Equal(12005, loaded.Skills.Single(s => s.Id == "s07").Xp);
            Assert.Equal(0, loaded.OverallXp);
            Assert.All(loaded.Categories, c => Assert.Equal(0, c.Xp));
            Assert.Equal(Rank.F, loaded.Rank.Current);
            var accepted = await Command(alice, new(Guid.NewGuid(), "accept", QuestId: quest.Id));
            await Command(alice, new(Guid.NewGuid(), "assess-skill", SkillId: "s07", Experience: Experience.New));
            var completed = await Command(alice, new(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id));
            Assert.Equal(35, completed.OverallXp);
            Assert.Equal(35, completed.Skills.Single(s => s.Id == "s07").Xp);
            var corrected = await Command(alice, new(Guid.NewGuid(), "assess-skill", SkillId: "s07", Experience: Experience.Practiced));
            Assert.Equal(440, corrected.Skills.Single(s => s.Id == "s07").Xp);
            Assert.Equal(35, corrected.OverallXp);
        }
    }

    /// <summary>Checks completed and reversed revision preservation, stale edits, and archival.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task EditingPreservesCompletedSnapshotsAndArchiveDoesNotEraseCommitments()
    {
        using var host = new Host(Connection);
        using var client = host.Client("alice");
        await Setup(client);
        var quest = new Quest(Guid.NewGuid().ToString(), "Original", "Outcome", "c07", Rank.F, Effort.Small, [], [], true);
        await Command(client, new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0));
        var accepted = await Command(client, new(Guid.NewGuid(), "accept", QuestId: quest.Id));
        var firstId = accepted.Occurrences.Single().Occurrence.Id;
        var complete = await Command(client, new(Guid.NewGuid(), "complete", firstId));
        var completionId = complete.Occurrences.Single().Completion!.Id;
        await Command(client, new(Guid.NewGuid(), "accept", QuestId: quest.Id));
        var edit = new QuestCommand(Guid.NewGuid(), "save-definition", Definition: quest with { Title = "Changed", Effort = Effort.Medium }, ExpectedRevision: 1);
        var revised = await Command(client, edit);
        Assert.Equal("Original", revised.Occurrences.Single(o => o.Occurrence.Id == firstId).Occurrence.Quest.Title);
        Assert.Equal("Changed", revised.Occurrences.Single(o => o.Occurrence.Id != firstId).Occurrence.Quest.Title);
        Assert.Equal(10, revised.OverallXp);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/commands", edit with { OperationId = Guid.NewGuid() })).StatusCode);
        await Command(client, new(Guid.NewGuid(), "undo", firstId, CompletionId: completionId));
        await Command(client, edit with { OperationId = Guid.NewGuid(), ExpectedRevision = 2 });
        await using var db = Context();
        var snapshot = JsonSerializer.Deserialize<Quest>((await db.Completions.SingleAsync()).QuestSnapshot!);
        Assert.Equal("Original", snapshot!.Title);
        var archived = await Command(client, new(Guid.NewGuid(), "archive-definition", QuestId: quest.Id, ExpectedRevision: 3));
        Assert.True(archived.Definitions.Single().Archived);
        Assert.Equal(2, archived.Occurrences.Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "accept", QuestId: quest.Id))).StatusCode);
        Assert.Equal(80, (await Command(client, new(Guid.NewGuid(), "complete", firstId))).OverallXp);
    }

    /// <summary>Checks independent parent/step rewards, cross-account links and planned-time semantics.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task LinkedGoalsAndPlanningKeepIndependentRewardsAndAccountOwnership()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("alice");
        using var bob = host.Client("bob");
        await Setup(alice);
        await Setup(bob);
        var parent = new Quest(Guid.NewGuid().ToString(), "Achieve goal", "The whole outcome is achieved", "c07", Rank.F, Effort.Large, [], [], true);
        await Command(alice, new(Guid.NewGuid(), "save-definition", Definition: parent, ExpectedRevision: 0));
        var parentId = (await Command(alice, new(Guid.NewGuid(), "accept", QuestId: parent.Id))).Occurrences.Single().Occurrence.Id;
        var state = await Command(alice, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"));
        var stepId = state.Occurrences.Single(o => o.Occurrence.Id != parentId).Occurrence.Id;
        var otherId = (await Command(bob, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"))).Occurrences.Single().Occurrence.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "link", otherId, ParentId: parentId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "link", parentId, ParentId: stepId))).StatusCode);
        await Command(alice, new(Guid.NewGuid(), "link", stepId, ParentId: parentId));
        var planned = await Command(alice, new(Guid.NewGuid(), "plan", stepId, DueDate: state.Today, PlannedTime: "00:01"));
        Assert.Equal("00:01", planned.Occurrences.Single(o => o.Occurrence.Id == stepId).Occurrence.PlannedTime);
        var stepComplete = await Command(alice, new(Guid.NewGuid(), "complete", stepId));
        Assert.Equal(10, stepComplete.OverallXp);
        Assert.Equal(QuestStatus.Active, stepComplete.Occurrences.Single(o => o.Occurrence.Id == parentId).Status);
        var finish = new QuestCommand(Guid.NewGuid(), "complete", parentId);
        Assert.Equal(810, (await Command(alice, finish)).OverallXp);
        Assert.Equal(810, (await Command(alice, finish)).OverallXp);
    }

    /// <summary>Checks weighted access and invalid request rollback rather than trusting mobile controls.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task InvalidCustomFieldsAndZeroWeightSkillsCannotBypassGates()
    {
        using var host = new Host(Connection);
        using var client = host.Client("alice");
        await Setup(client);
        await Command(client, new(Guid.NewGuid(), "assess-skill", SkillId: "s07", Experience: Experience.Expert));
        var quest = new Quest(Guid.NewGuid().ToString(), "Valid", "Done", "c04", Rank.A, Effort.Small, [], [new("s07", 10000)], true);
        Quest[] invalid = [quest with { Title = " " }, quest with { CategoryId = "invented" }, quest with { Skills = [new("s07", 10000), new("s08", 0)] }, quest with { Attributes = [new("a01", 9999)] }, quest with { IsCustom = false }];
        foreach (var value in invalid)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-definition", Definition: value, ExpectedRevision: 0))).StatusCode);
        Assert.Empty((await Read(client)).Definitions);
        await using var db = Context();
        Assert.Equal(1, await db.Operations.CountAsync());
    }
}
