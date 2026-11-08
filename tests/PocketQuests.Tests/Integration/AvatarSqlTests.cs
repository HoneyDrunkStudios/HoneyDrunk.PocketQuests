using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Integration;

/// <content>Avatar HTTP commands exercise actual persistence, receipts and reward reversal.</content>
public sealed partial class SqlApiTests
{
    /// <summary>Combines avatar versions, names, reward reversal and replay in one disposable database.</summary>
    /// <returns>The completed integration regression.</returns>
    [Fact]
    public async Task AvatarIntegration_PreservesNamesProgressRewardsAndHistory()
    {
        await CharacterSelection_PreservesProgressAndExactReplayAcrossRestart();
        foreach (var code in new[] { "v1:5:3:2:0:0", "v2:5:7:7:5:5:2:2", "v2:1:5:4:4:0:3:0", "v2:2:0:3:5:4:4:1", "v2:3:1:7:0:5:5:2", "v2:4:5:2:5:4:6:0", "v3:5:7:6:5:4:6:2:2:7", "v3:1:1:1:4:5:3:1:1:2", "v4:5:7:6:5:4:6:2:2:7:0:0", "v5:20000000000:00000", "v6:200000300000:00000", "v6:200000300001:00000", "v6:200000300201:00000" })
            await AvatarSaveSurvivesRestartAndIsAccountIsolated(code);
        await AvatarMilestonesFollowCustomXpRankAndUndo();
    }

    /// <summary>Persists free customization across restart and rejects locked items and payload substitution.</summary>
    /// <returns>A task completing after all persistence and isolation assertions.</returns>
    /// <param name="code">A legacy or expanded appearance.</param>
    private async Task AvatarSaveSurvivesRestartAndIsAccountIsolated(string code)
    {
        var legacy = new QuestCommand(Guid.Parse("1d6bbd0a-7afc-4852-bf9f-f77511393129"), QuestActions.FinishOnboarding);
        var save = new QuestCommand(code == "v1:5:3:2:0:0" ? Guid.Parse("30000000-0000-4000-8000-000000000001") : Guid.NewGuid(), QuestActions.SaveAvatar, AvatarCode: code);
        using (var host = new Host(Connection))
        using (var client = host.Client("avatar-alice"))
        {
            await Setup(client);
            await Command(client, legacy);
            var saved = await Command(client, save);
            Assert.Equal(save.AvatarCode, saved.Profile.AvatarCode);
            Assert.Equal(0, saved.OverallXp);
            using var locked = await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), QuestActions.SaveAvatar, AvatarCode: "v1:5:3:2:3:3"), Json);
            Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);
            using var changed = await client.PostAsJsonAsync("/api/commands", save with { AvatarCode = "v1:0:0:0:0:0" }, Json);
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        }

        using (var host = new Host(Connection))
        using (var alice = host.Client("avatar-alice"))
        using (var bob = host.Client("avatar-bob"))
        {
            await Setup(bob);
            Assert.Equal(save.AvatarCode, (await Read(alice)).Profile.AvatarCode);
            Assert.Equal(save.AvatarCode, (await Command(alice, save)).Profile.AvatarCode);
            Assert.Null((await Read(bob)).Profile.AvatarCode);
            Assert.Null((await Command(alice, legacy)).Profile.AvatarCode);
            using var ownership = await bob.PostAsJsonAsync("/api/commands", save, Json);
            Assert.Equal(HttpStatusCode.Conflict, ownership.StatusCode);
        }

        await using var db = Context();
        var oldReceipt = await db.Read.Set<CommandReceiptEntity>().SingleAsync(row => row.Id == legacy.OperationId);
        Assert.Equal(1, oldReceipt.DigestVersion);
        Assert.Equal("0F9C197CC41564B4A68341F1753EE8EF2AFD54B84571CA3E16672F6FD78A4E33", Convert.ToHexString(oldReceipt.PayloadDigest));
        var avatarReceipt = await db.Read.Set<CommandReceiptEntity>().SingleAsync(row => row.Id == save.OperationId);
        Assert.Equal(3, avatarReceipt.DigestVersion);
        if (code == "v1:5:3:2:0:0")
        {
            // Frozen tuple from the earlier avatar v2 protocol, before name/portrait integration.
            var legacyDigest = Convert.FromHexString("543EAB1E08549DC78A3F1EF06C7E5C218CBF40A47D2E6633DC1A0826B5A74CBF");
            await db.Read.Database.ExecuteSqlRawAsync("UPDATE [pocketquests].[CommandReceipt] SET [DigestVersion]=2,[PayloadDigest]={0} WHERE [Id]={1}", legacyDigest, save.OperationId);
            using var legacyHost = new Host(Connection);
            using var legacyClient = legacyHost.Client("avatar-alice");
            Assert.Equal(code, (await Command(legacyClient, save)).Profile.AvatarCode);
            var named = await Command(legacyClient, new(Guid.NewGuid(), QuestActions.SaveCharacter, CharacterName: "Rowan traveler", PortraitId: "portrait-3"));
            Assert.Equal(code, named.Profile.AvatarCode);
            Assert.Equal("Rowan traveler", named.Profile.CharacterName);
            Assert.Equal(code, (await Command(legacyClient, save)).Profile.AvatarCode);
            Assert.Equal("Rowan traveler", (await Read(legacyClient)).Profile.CharacterName);
        }

        foreach (var invalid in new[] { "v2:5:8:7:5:5:2:2", "v2:5:7:7:6:5:2:2", "v2:5:7:7:5:5:7:2", "v2:5:7:7:5:5:2:3", "v3:5:7:6:5:4:6:2:3:7", "v3:5:7:6:5:4:6:2:2:8", "v4:5:7:6:5:4:6:2:2:7:2:0", "v4:5:7:6:5:4:6:2:2:7:0:3", "v5:20000000000:30000", "v5:20000000000:07000", "v6:200000400001:00000", "v6:200000300002:00000", "v6:200000300301:00000" })
        {
            var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.Read.Database.ExecuteSqlRawAsync("UPDATE [pocketquests].[Account] SET [AvatarCode] = {0}", invalid));
            Assert.Equal(547, exception.Number);
        }
    }

    /// <summary>Custom XP earns decor and rank unlocks; replay and Undo preserve the intended eligibility.</summary>
    /// <returns>A task completing after real SQL replay assertions.</returns>
    private async Task AvatarMilestonesFollowCustomXpRankAndUndo()
    {
        using var host = new Host(Connection);
        using var client = host.Client("avatar-milestones");
        await Setup(client);
        async Task<QuestCommand> AcceptCustom(string category)
        {
            var quest = new Quest(Guid.NewGuid().ToString("D"), "Make something", "Finish a project", category, Rank.F, Effort.Medium, [], [], true);
            await Command(client, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0));
            var accepted = await Command(client, new(Guid.NewGuid(), QuestActions.Accept, QuestId: quest.Id));
            var id = accepted.Occurrences.Single(view => view.Occurrence.Quest.Id == quest.Id).Occurrence.Id;
            return new(Guid.NewGuid(), QuestActions.Complete, id);
        }

        const string decor = "v5:47600612701:01000";
        const string outfitAndRoom = "v5:47610612701:11000";
        var first = await AcceptCustom("c03");
        Assert.Equal(80, (await Command(client, first)).OverallXp);

        // Simulate history created before the milestone rules were introduced.
        await using (var db = Context())
            await db.Read.Database.ExecuteSqlInterpolatedAsync($"UPDATE pocketquests.QuestCommandHistory SET RulesetVersion = '1.0' WHERE AccountId = (SELECT AccountId FROM pocketquests.QuestCommandHistory WHERE Id = {first.OperationId})");
        var historical = await Command(client, first);
        Assert.Equal(80, historical.OverallXp);
        Assert.DoesNotContain(historical.Entitlements, reward => reward.Id.StartsWith("PQ-LEVEL-", StringComparison.Ordinal));
        Assert.Contains(historical.Entitlements, reward => reward.Id == "PQ-CAT-A01" && reward.Earned);

        var level = await Command(client, await AcceptCustom("c03"));
        Assert.Equal(2, level.OverallLevel);
        Assert.Contains(level.CompletionOutcome!.Unlocks, reward => reward.Id == "PQ-LEVEL-02");
        Assert.DoesNotContain(level.Entitlements, reward => reward.Id == "PQ-RANK-E" && reward.Earned);
        Assert.Equal(decor, (await Command(client, new(Guid.NewGuid(), QuestActions.SaveAvatar, AvatarCode: decor))).Profile.AvatarCode);
        using (var locked = await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), QuestActions.SaveAvatar, AvatarCode: outfitAndRoom), Json))
            Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);

        await Command(client, await AcceptCustom("c07"));
        var final = await AcceptCustom("c07");
        var completed = await Command(client, final);
        Assert.Equal(320, completed.OverallXp);
        Assert.Contains(completed.CompletionOutcome!.Unlocks, reward => reward.Id == "PQ-RANK-E");
        var completion = completed.Occurrences.Single(view => view.Occurrence.Id == final.OccurrenceId).Completion!.Id;
        var save = new QuestCommand(Guid.NewGuid(), QuestActions.SaveAvatar, AvatarCode: outfitAndRoom);
        Assert.Equal(save.AvatarCode, (await Command(client, save)).Profile.AvatarCode);
        var undone = await Command(client, new(Guid.NewGuid(), QuestActions.Undo, final.OccurrenceId, CompletionId: completion));
        Assert.Equal(decor, undone.Profile.AvatarCode);
        Assert.Equal(save.AvatarCode, (await Command(client, save)).Profile.AvatarCode);
        Assert.Equal(decor, (await Read(client)).Profile.AvatarCode);
        var recompleted = await Command(client, final with { OperationId = Guid.NewGuid() });
        Assert.Contains(recompleted.Entitlements, reward => reward.Id == "PQ-RANK-E" && reward.Earned);
        Assert.Equal(decor, recompleted.Profile.AvatarCode);
        Assert.DoesNotContain((await Command(client, first)).Entitlements, reward => reward.Id == "PQ-RANK-E");
    }
}
