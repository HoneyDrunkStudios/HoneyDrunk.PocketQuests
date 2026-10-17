using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Models.Quests;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketQuests.Tests.Integration;

/// <summary>Product HTTP regressions run against real SQL in both package and source Identity lanes.</summary>
public sealed partial class SqlApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Verifies sql state survives api restart and undo is durable.</summary>
    /// <returns>A task completing after the regression checks.</returns>
    [Fact]
    public async Task SqlStateSurvivesApiRestartAndUndoIsDurable()
    {
        Guid occurrence;
        Guid completion;
        var completeOperation = Guid.NewGuid();
        using (var host = new Host(Connection))
        using (var client = host.Client("alice"))
        {
            await Setup(client);
            var accepted = await Command(client, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01"));
            occurrence = accepted.Occurrences.Single().Occurrence.Id;
            var complete = await Command(client, new(completeOperation, "complete", occurrence));
            Assert.Equal(10, complete.OverallXp);
            completion = complete.Occurrences.Single().Completion!.Id;
        }

        // Entire web host, DI scopes and DbContexts are recreated. SQL remains.
        using (var host = new Host(Connection))
        using (var client = host.Client("alice"))
        {
            Assert.Equal(10, (await Read(client)).OverallXp);
            var replay = await Command(client, new(completeOperation, "complete", occurrence));
            Assert.Equal(completion, replay.Occurrences.Single().Completion!.Id);
            var undo = await Command(client, new(Guid.NewGuid(), "undo", occurrence, CompletionId: completion));
            Assert.Equal(0, undo.OverallXp);
        }

        using (var host = new Host(Connection))
        using (var client = host.Client("alice"))
            Assert.Equal(0, (await Read(client)).OverallXp);
        await using var db = Context();
        Assert.Equal(1, await db.Read.Set<QuestCompletionEntity>().CountAsync());
        Assert.Equal(1, await db.Read.Set<QuestOccurrenceEventEntity>().Where(e => e.EventCode == "Undone").CountAsync());
        Assert.Equal(3, await db.Audit.CountAsync());
    }

    /// <summary>Verifies concurrent hosts cannot double grant or accept same operation twice.</summary>
    /// <returns>A task completing after the regression checks.</returns>
    [Fact]
    public async Task ConcurrentHostsCannotDoubleGrantOrAcceptSameOperationTwice()
    {
        using var a = new Host(Connection);
        using var b = new Host(Connection);
        using var first = a.Client("alice");
        using var second = b.Client("alice");
        await Setup(first);
        var accept = new QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07");
        var results = await Task.WhenAll(Command(first, accept), Command(second, accept));
        var id = results[0].Occurrences.Single().Occurrence.Id;
        Assert.Equal(id, results[1].Occurrences.Single().Occurrence.Id);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Command(i % 2 == 0 ? first : second, new(Guid.NewGuid(), "complete", id))));
        Assert.Equal(10, (await Read(first)).OverallXp);
        await using var db = Context();
        Assert.Equal(1, await db.Read.Set<QuestOccurrenceEntity>().CountAsync());
        Assert.Equal(1, await db.Read.Set<QuestCompletionEntity>().CountAsync());
    }

    /// <summary>Verifies ownership and invalid operations fail without partial writes.</summary>
    /// <returns>A task completing after the regression checks.</returns>
    [Fact]
    public async Task OwnershipAndInvalidOperationsFailWithoutPartialWrites()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("alice");
        using var bob = host.Client("bob");
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        await Setup(alice);
        await Setup(bob);
        var accept = new QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01");
        var accepted = await Command(alice, accept);
        var id = accepted.Occurrences.Single().Occurrence.Id;
        Assert.Empty((await Read(bob)).Occurrences);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "complete", id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/commands", accept with { QuestId = "PQ-CAT-Q02" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "accept", QuestId: "invalid"))).StatusCode);
        await using var db = Context();
        Assert.Equal(1, await db.Read.Set<CommandReceiptEntity>().CountAsync());
        Assert.Equal(1, await db.Read.Set<QuestOccurrenceEntity>().CountAsync());
        Assert.Equal(0, await db.Read.Set<QuestCompletionEntity>().CountAsync());
    }

    /// <summary>Verifies a database failure rolls back both completion and receipt, then permits the same retry.</summary>
    /// <returns>A task completing after failure injection and recovery.</returns>
    [Fact]
    public async Task ReceiptWriteFailureRollsBackCompletionAndRetryRemainsSafe()
    {
        using var host = new Host(Connection);
        using var client = host.Client("alice");
        await Setup(client);
        var accepted = await Command(client, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01"));
        var command = new QuestCommand(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id);
        await using var db = Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTestReceipt ON pocketquests.CommandReceipt AFTER INSERT AS BEGIN THROW 51001, 'Injected receipt failure', 1; END;");
        try
        {
            using var rejected = await client.PostAsJsonAsync("/api/commands", command);
            Assert.Equal(HttpStatusCode.InternalServerError, rejected.StatusCode);
            Assert.DoesNotContain("Injected receipt failure", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await db.Read.Set<QuestCompletionEntity>().CountAsync());
            Assert.Equal(1, await db.Read.Set<CommandReceiptEntity>().CountAsync());
            Assert.Equal(1, await db.Audit.CountAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER pocketquests.RejectTestReceipt;");
        }

        Assert.Equal(10, (await Command(client, command)).OverallXp);
        Assert.Equal(10, (await Command(client, command)).OverallXp);
        Assert.Equal(1, await db.Read.Set<QuestCompletionEntity>().CountAsync());
        Assert.Equal(2, await db.Audit.CountAsync());
    }

    private static async Task Setup(HttpClient client) => (await client.PostAsJsonAsync("/api/profile", new { Zone = "America/New_York" })).EnsureSuccessStatusCode();

    private static async Task<QuestState> Read(HttpClient client) => (await client.GetFromJsonAsync<QuestState>("/api/state", Json))!;

    private static async Task<QuestState> Command(HttpClient client, QuestCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/commands", command);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<QuestState>(Json))!;
    }
}
