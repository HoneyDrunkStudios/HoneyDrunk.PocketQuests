using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Relational.Commands;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Commands;
using System.Data;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Real transactional lifecycle, private outbox ownership and retained original erasure-clock regressions.</summary>
/// <param name="fixture">Only a GUID-named database in the explicitly isolated schema-test instance.</param>
public sealed class RelationalLifecycleTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private RelationalQuestCommands Store => new(fixture.Connection);

    /// <summary>Private procedures work without table writes and compose with separately owned Outbox dispatcher grants.</summary>
    /// <returns>Completion after eight permission negatives and transition/acknowledgment/retention/erasure controls.</returns>
    [Fact]
    public Task PrivateLifecycleRoleAndSharedDispatcherPermissionsCompose() => fixture.Script("lifecycle-role-probes.sql");

    /// <summary>Renewed capabilities get acknowledgments without repeating effects; recovery preserves pause intent and invalidates prior proofs.</summary>
    /// <returns>Completion after monotonic fencing, proof invalidation, original receipts and explicit resume.</returns>
    [Fact]
    public async Task VersionsCapabilitiesAndRecoveryPreserveOneFreezeAndOriginalResponses()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        var original = await Store.Execute(owner, accept, Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var pausedAt = Start.AddMinutes(1);
        var inactive = Intent(owner, 1, IdentityProtocol.Inactive, pausedAt, pausedAt);
        await Store.ReceiveLifecycle(inactive, "private-ack", pausedAt);
        await Store.ReceiveLifecycle(inactive, "private-ack", pausedAt.AddSeconds(1));
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        Assert.Equal(2, account.MutationVersion);
        Assert.True(account.IsAccountPaused);
        Assert.Equal(10, await db.Set<AccountPauseEntity>().CountAsync(p => p.AccountId == account.Id));
        Assert.Equal(pausedAt, (await db.Set<QuestOccurrenceEntity>().SingleAsync(o => o.Id == accept.OccurrenceId)).FrozenAt);
        Assert.NotNull((await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id)).InvalidatedAt);
        Assert.Equal(1, await db.Set<LifecycleMessageEntity>().CountAsync(m => m.IdentityUserId == owner.Subject));
        var renewed = inactive with { Acknowledgment = Capability() };
        await Store.ReceiveLifecycle(renewed, "private-ack", pausedAt.AddSeconds(2));
        Assert.Equal(2, await db.Set<LifecycleMessageEntity>().CountAsync(m => m.IdentityUserId == owner.Subject));
        Assert.Equal(2, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == account.Id)).MutationVersion);
        await Assert.ThrowsAsync<SqlException>(() => Store.Execute(owner, accept, pausedAt));
        var recoveredAt = pausedAt.AddMinutes(2);
        var recovered = Intent(owner, 2, IdentityProtocol.Active, recoveredAt, pausedAt);
        await Store.ReceiveLifecycle(recovered, "private-ack", recoveredAt);
        await Store.ReceiveLifecycle(inactive, "private-ack", recoveredAt.AddSeconds(1));
        Assert.Equal(2, (await db.Set<AccountLifecycleStateEntity>().SingleAsync(b => b.IdentityUserId == owner.Subject)).Version);
        var state = await Store.Read(owner, recoveredAt);
        Assert.True(state.Schedule.AccountPaused);
        Assert.All(state.Schedule.Pauses, p => Assert.Equal(pausedAt, p.StartedAt));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await Store.Execute(owner, accept, recoveredAt)));
        var staleProof = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId, RecordedTime: new(anchor.Id, anchor.BootId, 1, 1000, Start.AddSeconds(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.Execute(owner, staleProof, recoveredAt));
        var resume = new QuestCommand(Guid.NewGuid(), QuestActions.Resume);
        var resumed = await Store.Execute(owner, resume, recoveredAt);
        Assert.False(resumed.Schedule.AccountPaused);
        Assert.Null(Assert.Single(resumed.Occurrences).Occurrence.Lifecycle!.FrozenAt);
        Assert.Equal(JsonSerializer.Serialize(resumed), JsonSerializer.Serialize(await Store.Execute(owner, resume, recoveredAt.AddYears(1))));
    }

    /// <summary>Authoritative recovery can arrive before inactive delivery; unchanged authentication avoids exclusive locks and writes.</summary>
    /// <returns>Completion after absent-account fences, stable versions and independent lock contention checks.</returns>
    [Fact]
    public async Task ResolutionFencesDelayedDeliveryAndUnchangedAuthenticationDoesNotWrite()
    {
        var owner = Identity();
        var pausedAt = Start.AddMinutes(1);
        await Store.ReceiveLifecycle(Intent(owner, 1, IdentityProtocol.Inactive, pausedAt, pausedAt), "private-ack", pausedAt);
        await Assert.ThrowsAsync<SqlException>(() => Store.Initialize(owner, "Etc/UTC", pausedAt));
        var active = new UserRecord(owner.Subject, IdentityProtocol.Active, Start, 2, pausedAt);
        await Store.ObserveActive(active, pausedAt.AddMinutes(1));
        await Store.Initialize(owner, "Etc/UTC", pausedAt.AddMinutes(1));
        await Store.Execute(owner, Accept(), pausedAt.AddMinutes(1));
        await using var db = fixture.Context();
        var before = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var barrier = await db.Set<AccountLifecycleStateEntity>().SingleAsync(b => b.IdentityUserId == owner.Subject);
        Assert.Equal(before.Id, barrier.AccountId);
        await using var connection = new SqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var sql = new SqlCommand("pocketquests.AcquireAccountLock", connection, transaction) { CommandType = CommandType.StoredProcedure };
        sql.Parameters.Add("@IdentityUserId", SqlDbType.VarChar, 30).Value = owner.Subject;
        await sql.ExecuteNonQueryAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Store.ObserveActive(active, pausedAt.AddMinutes(2), timeout.Token);
        Assert.Equal(before.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id)).RowVersion);
        Assert.Equal(barrier.RowVersion, (await db.Set<AccountLifecycleStateEntity>().SingleAsync(b => b.Id == barrier.Id)).RowVersion);
        await transaction.RollbackAsync();
        await Store.ReceiveLifecycle(Intent(owner, 1, IdentityProtocol.Inactive, pausedAt, pausedAt), "private-ack", pausedAt.AddMinutes(2));
        Assert.False((await Store.Read(owner, pausedAt.AddMinutes(2))).Schedule.AccountPaused);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.ObserveActive(active with { LifecycleVersion = 1 }, pausedAt.AddMinutes(2)));
    }

    /// <summary>An outbox insert failure rolls back freezes and erasure together with their owned Audit rows.</summary>
    /// <returns>Completion after deliberate SQL faults and successful retries.</returns>
    [Fact]
    public async Task AcknowledgmentFailureRollsBackFreezeAndErasure()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        await Store.Execute(owner, accept, Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        await using var db = fixture.Context();
        var before = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        await fixture.Execute("CREATE TRIGGER outbox.SchemaTestRejectAck ON outbox.OutboxMessages AFTER INSERT AS THROW 51999,'Synthetic acknowledgment insert failure.',1;");
        try
        {
            var inactive = Intent(owner, 1, IdentityProtocol.Inactive, Start.AddMinutes(1), Start.AddMinutes(1));
            Assert.Equal(51999, (await Assert.ThrowsAsync<SqlException>(() => Store.ReceiveLifecycle(inactive, "private-ack", Start.AddMinutes(1)))).Number);
            Assert.Equal(before.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id)).RowVersion);
            Assert.Null((await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id)).InvalidatedAt);
            Assert.False(await db.Set<AccountLifecycleStateEntity>().AnyAsync(b => b.IdentityUserId == owner.Subject));
            var erase = Intent(owner, 2, IdentityProtocol.Erasing, Start.AddDays(30), Start.AddMinutes(1));
            Assert.Equal(51999, (await Assert.ThrowsAsync<SqlException>(() => Store.ReceiveLifecycle(erase, "private-ack", Start.AddDays(30)))).Number);
            Assert.True(await db.Set<QuestOccurrenceEntity>().AnyAsync(o => o.Id == accept.OccurrenceId));
            Assert.True(await db.Set<AccountAuditRecordEntity>().AnyAsync(a => a.AccountId == before.Id));
            Assert.False(await db.Set<ErasureMarkerEntity>().AnyAsync(m => m.Id == owner.Subject));
        }
        finally
        {
            await fixture.Execute("DROP TRIGGER outbox.SchemaTestRejectAck;");
        }

        await Store.ReceiveLifecycle(Intent(owner, 2, IdentityProtocol.Erasing, Start.AddDays(30), Start.AddMinutes(1)), "private-ack", Start.AddDays(30));
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.Id == before.Id));
        Assert.Single(await db.Set<LifecycleMessageEntity>().Where(m => m.IdentityUserId == owner.Subject).ToListAsync());
    }

    private static AccountIdentity Identity() => new("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());

    private static QuestCommand Accept() => new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01");

    private static string Capability() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private static LifecycleIntent Intent(AccountIdentity owner, long version, string state, DateTimeOffset effectiveAt, DateTimeOffset pausedAt) =>
        new(owner.Subject, version, state, effectiveAt, effectiveAt.AddHours(1), IdentityProtocol.ConsumerId, Capability(), pausedAt);
}
