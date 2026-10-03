using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Definitions;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>SQL-backed custom skill isolation and history regressions.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Renaming keeps identity and earnings; archival preserves history and other accounts cannot reference it.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task CustomSkillsKeepEarnedXpAcrossRenamesAndArchival()
    {
        var skillId = Guid.NewGuid().ToString();
        var quest = new Quest(Guid.NewGuid().ToString(), "Practice", "Achieved outcome", "c07", Rank.A, Effort.Small, [], [new(skillId, 10000)], true);
        using (var host = new Host(Connection))
        using (var alice = host.Client("alice"))
        using (var bob = host.Client("bob"))
        {
            await Setup(alice);
            await Setup(bob);
            await Command(alice, new(Guid.NewGuid(), "save-skill", SkillId: skillId, SkillName: "  Pottery  ", ExpectedRevision: 0));
            Assert.Equal(HttpStatusCode.BadRequest, (await bob.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "assess-skill", SkillId: skillId, Experience: Experience.Expert))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-skill", SkillId: Guid.NewGuid().ToString(), SkillName: "pottery", ExpectedRevision: 0))).StatusCode);
            await Command(alice, new(Guid.NewGuid(), "assess-skill", SkillId: skillId, Experience: Experience.Expert));
            await Command(alice, new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0));
            var accepted = await Command(alice, new(Guid.NewGuid(), "accept", QuestId: quest.Id));
            await Command(alice, new(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id));
            await Command(alice, new(Guid.NewGuid(), "save-skill", SkillId: skillId, SkillName: "Ceramics", ExpectedRevision: 1));
            await Command(alice, new(Guid.NewGuid(), "assess-skill", SkillId: skillId, Experience: Experience.New));
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "archive-skill", SkillId: skillId, ExpectedRevision: 2))).StatusCode);
            await Command(alice, new(Guid.NewGuid(), "archive-definition", QuestId: quest.Id, ExpectedRevision: 1));
            await Command(alice, new(Guid.NewGuid(), "archive-skill", SkillId: skillId, ExpectedRevision: 2));
        }

        using var restarted = new Host(Connection);
        using var client = restarted.Client("alice");
        var state = await Read(client);
        Assert.True(state.Profile.CustomSkills!.Single().Archived);
        var balance = state.Skills.Single(s => s.Id == skillId);
        Assert.Equal("Ceramics", balance.Name);
        Assert.Equal(35, balance.Xp);
        Assert.Equal(35, state.OverallXp);
        Assert.Equal(2, state.Profile.AssessmentHistory!.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-definition", Definition: quest with { Id = Guid.NewGuid().ToString(), Rank = Rank.F }, ExpectedRevision: 0))).StatusCode);
    }
}
