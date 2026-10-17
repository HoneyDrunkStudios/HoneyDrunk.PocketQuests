using HoneyDrunk.Data.Outbox;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Authentication;
using HoneyDrunk.Identity.AccountLifecycle;
using HoneyDrunk.Identity.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
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
            await Lifecycle().ReceiveLifecycle(inactive, "identity-acks", clock.Now);
        await using (var db = IdentityContext())
            await Coordinator(db).Cancel(login, true);

        // No Active message has been delivered: the current Identity response must fence it.
        var recovered = await Read(alice);
        Assert.True(recovered.Schedule.AccountPaused);
        Assert.Equal(QuestStatus.Frozen, recovered.Occurrences.Single().Status);
        await using (var db = Context())
            await Lifecycle().ReceiveLifecycle(inactive, "identity-acks", clock.Now);
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
            var consumer = Lifecycle();
            await consumer.ReceiveLifecycle(erase, "identity-acks", clock.Now);
            await consumer.ReceiveLifecycle(erase, "identity-acks", clock.Now);
            Assert.Single(await db.Read.Set<ErasureMarkerEntity>().ToListAsync());
            Assert.Single(await db.Infrastructure.Set<OutboxMessage>().ToListAsync());
            Assert.Single(await db.Read.Set<AccountEntity>().ToListAsync());
            Assert.Empty(await db.Read.Set<QuestOccurrenceEntity>().ToListAsync());
            Assert.Empty(await db.Read.Set<QuestCompletionEntity>().ToListAsync());
            Assert.Empty(await db.Read.Set<QuestOccurrenceEventEntity>().Where(e => e.EventCode == "Undone").ToListAsync());
            Assert.Empty(await db.Read.Set<QuestDefinitionEntity>().ToListAsync());
            Assert.Empty(await db.Read.Set<QuestDefinitionRevisionEntity>().ToListAsync());
            Assert.Empty(await db.Read.Set<CommandReceiptEntity>().ToListAsync());
            Assert.Empty(await db.Read.Set<SyncAnchorEntity>().ToListAsync());
            Assert.DoesNotContain(await db.Audit.ToListAsync(), a => a.Actor == userId);
            var ack = JsonSerializer.Deserialize<LifecycleAck>((await db.Infrastructure.Set<OutboxMessage>().SingleAsync()).Payload)!;
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
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE outbox.OutboxMessages WITH NOCHECK ADD CONSTRAINT CK_SchemaTestRejectAck CHECK (Id IS NULL);");
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => Lifecycle().ReceiveLifecycle(intent, "identity-acks", clock.Now));
            Assert.Equal(547, Assert.IsType<SqlException>(failure.InnerException).Number);
        }
        finally
        {
            await using var db = Context();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE outbox.OutboxMessages DROP CONSTRAINT CK_SchemaTestRejectAck;");
        }

        await using (var db = Context())
        {
            Assert.Single(await db.Read.Set<AccountEntity>().ToListAsync());
            Assert.Single(await db.Read.Set<QuestOccurrenceEntity>().ToListAsync());
            Assert.Empty(await db.Read.Set<ErasureMarkerEntity>().ToListAsync());
            await Lifecycle().ReapplyErasure(userId, clock.Now, clock.Now);
            Assert.Empty(await db.Read.Set<AccountEntity>().ToListAsync());
            Assert.Empty(await db.Audit.ToListAsync());
            Assert.Single(await db.Read.Set<ErasureMarkerEntity>().ToListAsync());
        }

        // Even a still-active stale Identity response cannot recreate product data behind a marker.
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        await using (var db = Context())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store().Read(new AccountIdentity("honeydrunk-identity", userId), clock.Now, default));
    }

    private static async Task<LifecycleIntent> LatestIntent(IdentityDbContext db, string userId) =>
        (await db.Set<OutboxMessage>().Where(m => m.Payload.Contains(userId)).ToListAsync()).Select(m => JsonSerializer.Deserialize<LifecycleIntent>(m.Payload)!).OrderByDescending(m => m.Version).First();

    private sealed class LifecycleClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
