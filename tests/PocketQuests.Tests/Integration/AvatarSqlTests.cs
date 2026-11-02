using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Avatar HTTP commands exercise actual persistence, receipts and reward reversal.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Combines avatar versions, names, reward reversal and replay in one disposable database.</summary>
    /// <returns>The completed integration regression.</returns>
    [Fact]
    public async Task AvatarIntegration_PreservesNamesProgressRewardsAndHistory()
    {
        await CharacterSelection_PreservesProgressAndExactReplayAcrossRestart();
        foreach (var code in new[] { "v1:5:3:2:0:0", "v2:5:7:7:5:5:2:2", "v2:1:5:4:4:0:3:0", "v2:2:0:3:5:4:4:1", "v2:3:1:7:0:5:5:2", "v2:4:5:2:5:4:6:0" })
            await AvatarSaveSurvivesRestartAndIsAccountIsolated(code);
        await AvatarUndoAndRecompletionPreserveHistoricalSelection();
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

        foreach (var invalid in new[] { "v2:5:8:7:5:5:2:2", "v2:5:7:7:6:5:2:2", "v2:5:7:7:5:5:7:2", "v2:5:7:7:5:5:2:3" })
        {
            var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.Read.Database.ExecuteSqlRawAsync("UPDATE [pocketquests].[Account] SET [AvatarCode] = {0}", invalid));
            Assert.Equal(547, exception.Number);
        }
    }

    /// <summary>Undo removes an equipped cosmetic; old receipt replay remains exact and re-earning does not equip it.</summary>
    /// <returns>A task completing after real SQL replay assertions.</returns>
    private async Task AvatarUndoAndRecompletionPreserveHistoricalSelection()
    {
        using var host = new Host(Connection);
        using var client = host.Client("avatar-rewards");
        await Setup(client);
        var quest = new Quest(Guid.NewGuid().ToString("D"), "Make something", "Finish a small project", "c03", Rank.F, Effort.Small, [], [], true);
        await Command(client, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0));
        var accepted = await Command(client, new(Guid.NewGuid(), QuestActions.Accept, QuestId: quest.Id));
        var id = Assert.Single(accepted.Occurrences).Occurrence.Id;
        var completed = await Command(client, new(Guid.NewGuid(), QuestActions.Complete, id));
        var completion = Assert.Single(completed.Occurrences).Completion!.Id;
        var save = new QuestCommand(Guid.NewGuid(), QuestActions.SaveAvatar, AvatarCode: "v2:4:7:6:1:0:6:1");
        Assert.Equal(save.AvatarCode, (await Command(client, save)).Profile.AvatarCode);
        var undone = await Command(client, new(Guid.NewGuid(), QuestActions.Undo, id, CompletionId: completion));
        Assert.Equal("v2:4:7:6:0:0:6:1", undone.Profile.AvatarCode);
        Assert.Equal(save.AvatarCode, (await Command(client, save)).Profile.AvatarCode);
        Assert.Equal("v2:4:7:6:0:0:6:1", (await Read(client)).Profile.AvatarCode);
        var recompleted = await Command(client, new(Guid.NewGuid(), QuestActions.Complete, id));
        Assert.Contains(recompleted.Entitlements, reward => reward.Id == "PQ-CAT-A01" && reward.Earned);
        Assert.Equal("v2:4:7:6:0:0:6:1", recompleted.Profile.AvatarCode);
    }
}
