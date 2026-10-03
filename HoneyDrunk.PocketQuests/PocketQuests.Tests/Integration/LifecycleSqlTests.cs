using HoneyDrunk.Data.Outbox;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Authentication;
using HoneyDrunk.Identity.AccountLifecycle;
using HoneyDrunk.Identity.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PocketQuests.Application.Identity;
using PocketQuests.Data.AccountLifecycle;
using PocketQuests.Data.Repositories;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Occurrences;
using System.Net;
using System.Text.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Cross-service lifecycle acceptance against isolated real Identity and product databases.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Inactive access fails immediately, recovery freezes before delayed delivery, and verified erasure clears only its owner.</summary>
    /// <returns>The complete local lifecycle acceptance test.</returns>
    [Fact]
    public async Task DeletionRecoveryFenceAndConsumerErasureAreAtomic()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("alice");
        using var bob = host.Client("bob");
        await Setup(alice);
        await Setup(bob);
        var custom = new Quest(Guid.NewGuid().ToString(), "Private test quest", "A chosen action is finished", "c04", Rank.F, Effort.Small, [], [new("s07", 10000)], true);
        await Command(alice, new(Guid.NewGuid(), "save-definition", Definition: custom, ExpectedRevision: 0));
        var accepted = await Command(alice, new(Guid.NewGuid(), "accept", QuestId: custom.Id));
        var occurrence = accepted.Occurrences.Single().Occurrence.Id;
        var completed = await Command(alice, new(Guid.NewGuid(), "complete", occurrence));
        await Command(alice, new(Guid.NewGuid(), "undo", occurrence, CompletionId: completed.Occurrences.Single().Completion!.Id));
        var clock = new LifecycleClock { Now = DateTimeOffset.UtcNow };
        string userId;
        await using (var db = IdentityContext())
            userId = (await db.Subjects.SingleAsync(s => s.Subject == "alice")).UserId;
        var login = new VerifiedLogin(new("https://identity.test", "alice"), clock.Now);
        LifecycleIntent inactive;
        await using (var db = IdentityContext())
        {
            await Coordinator(db).Request(login, true);
            inactive = await LatestIntent(db, userId);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        await using (var db = Context())
            await new SqlQuestLifecycle(db, clock).Receive(inactive, "identity-acks");
        await using (var db = IdentityContext())
            await Coordinator(db).Cancel(login, true);

        // No Active message has been delivered: the current Identity response must fence it.
        var recovered = await Read(alice);
        Assert.True(recovered.Schedule.AccountPaused);
        Assert.Equal(QuestStatus.Frozen, recovered.Occurrences.Single().Status);
        await using (var db = Context())
            await new SqlQuestLifecycle(db, clock).Receive(inactive, "identity-acks");
        Assert.True((await Read(alice)).Schedule.AccountPaused);
        await Command(alice, new(Guid.NewGuid(), "resume"));
        Assert.Equal(10, (await Command(alice, new(Guid.NewGuid(), "complete", occurrence))).OverallXp);

        await using (var db = IdentityContext())
            await Coordinator(db).Request(login, true);
        clock.Now = clock.Now.AddDays(30);
        await using (var db = IdentityContext())
            await Coordinator(db).Process(userId);
        LifecycleIntent erase;
        await using (var db = IdentityContext())
            erase = await LatestIntent(db, userId);
        Assert.Equal("Erasing", erase.State);
        await using (var db = Context())
        {
            var consumer = new SqlQuestLifecycle(db, clock);
            await consumer.Receive(erase, "identity-acks");
            await consumer.Receive(erase, "identity-acks");
            Assert.Single(await db.Erasures.ToListAsync());
            Assert.Single(await db.Set<OutboxMessage>().ToListAsync());
            Assert.Single(await db.Accounts.ToListAsync());
            Assert.Empty(await db.Occurrences.ToListAsync());
            Assert.Empty(await db.Completions.ToListAsync());
            Assert.Empty(await db.Undos.ToListAsync());
            Assert.Empty(await db.Definitions.ToListAsync());
            Assert.Empty(await db.DefinitionRevisions.ToListAsync());
            Assert.Empty(await db.Operations.ToListAsync());
            Assert.Empty(await db.SyncAnchors.ToListAsync());
            Assert.DoesNotContain(await db.Audit.ToListAsync(), a => a.Actor == userId);
            var ack = JsonSerializer.Deserialize<LifecycleAck>((await db.Set<OutboxMessage>().SingleAsync()).Payload)!;
            await using var identity = IdentityContext();
            await Coordinator(identity).Acknowledge(ack);
        }

        clock.Now = clock.Now.AddMinutes(2);
        await using (var db = IdentityContext())
        {
            await Coordinator(db).Process(userId);
            Assert.Equal(userId, (await db.Erasures.SingleAsync()).UserId);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        Assert.Equal(0, (await Read(bob)).OverallXp);
        using var fresh = host.Client("fresh-provider-object");
        await Setup(fresh);
        Assert.Equal(0, (await Read(fresh)).OverallXp);

        SqlAccountLifecycle Coordinator(IdentityDbContext db) => new(db, clock, host.External, Options.Create(new LifecycleOptions { DeliveryEnabled = true, Consumers = { ["pocketquests"] = "pocketquests-lifecycle" } }));
    }

    /// <summary>Injected acknowledgment failure rolls back erasure; restore markers purge restored rows before access.</summary>
    /// <returns>The atomicity and restore acceptance test.</returns>
    [Fact]
    public async Task ErasureRollbackAndRestoreMarkerPreserveTheDeletionBoundary()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("alice");
        await Setup(alice);
        await Command(alice, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"));
        string userId;
        await using (var identity = IdentityContext())
            userId = (await identity.Subjects.SingleAsync()).UserId;
        var clock = new LifecycleClock { Now = DateTimeOffset.UtcNow };
        var intent = new LifecycleIntent(userId, 1, "Erasing", clock.Now, clock.Now.AddHours(1), "pocketquests", new string('a', 64), clock.Now.AddDays(-30));
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectLifecycleAck ON outbox.OutboxMessages AFTER INSERT AS BEGIN THROW 51001, 'Injected acknowledgment failure', 1; END;");
        await using (var db = Context())
            await Assert.ThrowsAsync<DbUpdateException>(() => new SqlQuestLifecycle(db, clock).Receive(intent, "identity-acks"));
        await using (var db = Context())
        {
            Assert.Single(await db.Accounts.ToListAsync());
            Assert.Single(await db.Occurrences.ToListAsync());
            Assert.Empty(await db.Erasures.ToListAsync());
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER outbox.RejectLifecycleAck;");
            await new SqlQuestLifecycle(db, clock).ReapplyErasure(new() { UserId = userId, ErasedAt = clock.Now });
            Assert.Empty(await db.Accounts.ToListAsync());
            Assert.Empty(await db.Audit.ToListAsync());
            Assert.Single(await db.Erasures.ToListAsync());
        }

        // Even a still-active stale Identity response cannot recreate product data behind a marker.
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        await using (var db = Context())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new SqlQuestStore(db).Read(new AccountIdentity("honeydrunk-identity", userId), "UTC", clock.Now, default));
    }

    private static async Task<LifecycleIntent> LatestIntent(IdentityDbContext db, string userId) =>
        (await db.Set<OutboxMessage>().Where(m => m.Payload.Contains(userId)).ToListAsync()).Select(m => JsonSerializer.Deserialize<LifecycleIntent>(m.Payload)!).OrderByDescending(m => m.Version).First();

    private sealed class LifecycleClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
