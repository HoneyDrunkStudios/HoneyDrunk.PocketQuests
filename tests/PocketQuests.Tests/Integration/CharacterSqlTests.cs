using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Quests;
using System.Net;

namespace PocketQuests.Tests.Integration;

/// <content>Account-owned character persistence, replay and progression preservation.</content>
public sealed partial class SqlApiTests
{
    /// <summary>Character choices survive restart without changing quest progress or another account.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task CharacterSelection_PreservesProgressAndExactReplayAcrossRestart()
    {
        var save = new QuestCommand(Guid.NewGuid(), "save-character", CharacterName: " Rowan ", PortraitId: "portrait-2");
        long earned;
        using (var host = new Host(Connection))
        using (var alice = host.Client("character-alice"))
        using (var bob = host.Client("character-bob"))
        {
            await Setup(alice);
            await Setup(bob);
            var accepted = await Command(alice, new(Guid.NewGuid(), "accept", QuestId: Catalog.Quests[0].Id));
            var completed = await Command(alice, new(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id));
            earned = completed.OverallXp;
            var chosen = await Command(alice, save);
            Assert.Equal("Rowan", chosen.Profile.CharacterName);
            Assert.Equal("portrait-2", chosen.Profile.PortraitId);
            Assert.Equal(earned, chosen.OverallXp);
            Assert.Equivalent(completed.Occurrences, chosen.Occurrences, strict: true);
            Assert.Null((await Read(bob)).Profile.CharacterName);
            Assert.Equal("Rowan", (await Command(alice, save)).Profile.CharacterName);
            await Command(alice, new(Guid.NewGuid(), "save-character", CharacterName: "Juniper", PortraitId: "portrait-6"));
            Assert.Equal("Rowan", (await Command(alice, save)).Profile.CharacterName);
            Assert.Equal("Juniper", (await Read(alice)).Profile.CharacterName);
            Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/commands", save with { CharacterName = "Different" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/commands", save with { PortraitId = "portrait-1" })).StatusCode);

            // Exercise invalid requests on the same isolated SQL fixture; each must leave Bob unchanged.
            (string? name, string? portrait)[] invalid = [(name: string.Empty, portrait: "portrait-1"), ("   ", "portrait-1"), ("A\nB", "portrait-1"), (new string('A', 41), "portrait-1"), (null, "portrait-1"), ("Valid", null), ("Valid", "https://untrusted.example/avatar.jpg"), ("Valid", "portrait-7")];
            foreach (var (name, portrait) in invalid)
            {
                var result = await bob.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "save-character", CharacterName: name, PortraitId: portrait));
                Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
                Assert.Null((await Read(bob)).Profile.CharacterName);
            }
        }

        using (var host = new Host(Connection))
        using (var alice = host.Client("character-alice"))
        {
            var loaded = await Read(alice);
            Assert.Equal("Juniper", loaded.Profile.CharacterName);
            Assert.Equal("portrait-6", loaded.Profile.PortraitId);
            Assert.Equal(earned, loaded.OverallXp);
            Assert.Equal(QuestStatus.Completed, loaded.Occurrences.Single().Status);
        }
    }
}
