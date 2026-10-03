using HoneyDrunk.Data.Outbox;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Context;
using PocketQuests.Data.Entities;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PocketQuests.Data.AccountLifecycle;

/// <summary>Private Identity lifecycle consumer with account-locked mutations and atomic acknowledgment outbox.</summary>
public sealed class SqlQuestLifecycle(QuestDbContext db, TimeProvider clock)
{
    /// <summary>Fences delayed cancellation delivery using the current authoritative Identity response.</summary>
    /// <param name="user">Identity's validated active account; never a public request body.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion after any required freeze is durable.</returns>
    public async Task ObserveActive(UserRecord user, CancellationToken token = default)
    {
        if (user.State != IdentityProtocol.Active || (user.LifecycleVersion > 0 && user.DeletionPausedAt is null))
            throw new UnauthorizedAccessException("Account is inactive or its lifecycle is incomplete.");
        await using var tx = await Lock(user.UserId, token);
        if (await db.Erasures.AnyAsync(m => m.UserId == user.UserId, token))
            throw new UnauthorizedAccessException("This account was erased.");
        var barrier = await db.LifecycleBarriers.SingleOrDefaultAsync(b => b.UserId == user.UserId, token);
        if (barrier is not null && (barrier.Version > user.LifecycleVersion || (barrier.Version == user.LifecycleVersion && barrier.State != IdentityProtocol.Active)))
            throw new UnauthorizedAccessException("Account lifecycle changed; sign in again.");
        if (user.LifecycleVersion > (barrier?.Version ?? 0))
            await Apply(user.UserId, user.LifecycleVersion, IdentityProtocol.Active, user.DeletionPausedAt!.Value, barrier, token);
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        db.ChangeTracker.Clear();
    }

    /// <summary>Consumes a registered, unexpired lifecycle instruction and acknowledges only after committing.</summary>
    /// <param name="intent">Instruction from the private Identity-only sender endpoint.</param>
    /// <param name="acknowledgmentQueue">Trusted host configuration; never taken from the message.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The atomic account mutation and outbox write.</returns>
    public async Task Receive(LifecycleIntent intent, string acknowledgmentQueue, CancellationToken token = default)
    {
        if (intent.Consumer != IdentityProtocol.ConsumerId || intent.State is not (IdentityProtocol.Active or IdentityProtocol.Inactive or IdentityProtocol.Erasing) || intent.Version <= 0
            || intent.Acknowledgment.Length != 64 || string.IsNullOrWhiteSpace(acknowledgmentQueue))
            throw new UnauthorizedAccessException("Invalid lifecycle instruction.");
        if (clock.GetUtcNow() >= intent.ExpiresAt)
            return;
        if (intent.EffectiveAt > clock.GetUtcNow().AddSeconds(30) || intent.PausedAt > intent.EffectiveAt || intent.ExpiresAt > clock.GetUtcNow().AddHours(1).AddSeconds(30))
            throw new ArgumentException("Invalid lifecycle clock.");
        await using var tx = await Lock(intent.UserId, token);
        var marker = await db.Erasures.SingleOrDefaultAsync(m => m.UserId == intent.UserId, token);
        var barrier = await db.LifecycleBarriers.SingleOrDefaultAsync(b => b.UserId == intent.UserId, token);
        if (marker is null && intent.Version > (barrier?.Version ?? 0))
        {
            if (intent.State == IdentityProtocol.Erasing)
            {
                await Purge(intent.UserId, token);
                db.Erasures.Add(new() { UserId = intent.UserId, ErasedAt = clock.GetUtcNow() });
            }
            else
            {
                await Apply(intent.UserId, intent.Version, intent.State, intent.PausedAt, barrier, token);
            }
        }

        var ack = new LifecycleAck(intent.UserId, intent.Version, IdentityProtocol.ConsumerId, intent.Acknowledgment);
        var receiptId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{intent.UserId}/{intent.Version}/{intent.Acknowledgment}")).AsSpan(0, 16));
        if (!await db.Set<OutboxMessage>().AnyAsync(m => m.Id == receiptId, token))
        {
            db.Set<OutboxMessage>().Add(new()
            {
                Id = receiptId, Type = typeof(LifecycleAck).AssemblyQualifiedName!, Payload = JsonSerializer.Serialize(ack), OccurredAt = clock.GetUtcNow(),
                TenantId = "internal", CorrelationId = Guid.NewGuid().ToString("N"), Headers = JsonSerializer.Serialize(new Dictionary<string, string> { [OutboxHeaderNames.Destination] = acknowledgmentQueue }),
            });
        }

        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        db.ChangeTracker.Clear();
    }

    /// <summary>Reapplies a current external marker before a restored database becomes available.</summary>
    /// <param name="marker">Verified marker from outside the restored backup.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The scoped data and audit purge.</returns>
    public async Task ReapplyErasure(ErasureMarkerEntity marker, CancellationToken token = default)
    {
        if (marker.ErasedAt > clock.GetUtcNow() || marker.ErasedAt <= clock.GetUtcNow().AddDays(-35))
            throw new ArgumentException("A current verified marker is required.");
        await using var tx = await Lock(marker.UserId, token);
        await Purge(marker.UserId, token);
        var existing = await db.Erasures.SingleOrDefaultAsync(m => m.UserId == marker.UserId, token);
        if (existing is null)
            db.Erasures.Add(new() { UserId = marker.UserId, ErasedAt = marker.ErasedAt });
        else
            existing.ErasedAt = marker.ErasedAt;
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        db.ChangeTracker.Clear();
    }

    /// <summary>Expires minimal markers after 35 days and removes short-lived delivery envelopes.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>The retention cleanup.</returns>
    public async Task Prune(CancellationToken token = default)
    {
        var markerCutoff = clock.GetUtcNow().AddDays(-35);
        var envelopeCutoff = clock.GetUtcNow().AddHours(-1);
        await db.Erasures.Where(m => m.ErasedAt <= markerCutoff).ExecuteDeleteAsync(token);
        await db.Set<OutboxMessage>().Where(m => m.Type == typeof(LifecycleAck).AssemblyQualifiedName && (m.OccurredAt <= envelopeCutoff || m.Status == OutboxMessageStatus.Dispatched)).ExecuteDeleteAsync(token);
    }

    internal static string IdentityKey(string userId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new AccountIdentity("honeydrunk-identity", userId)))));

    private async Task<IDbContextTransaction> Lock(string userId, CancellationToken token)
    {
        if (userId.Length != 30 || !userId.StartsWith("usr_", StringComparison.Ordinal))
            throw new ArgumentException("Canonical account ID is required.");
        var tx = await db.Database.BeginTransactionAsync(token);
        try
        {
            var resource = "pocketquests:" + IdentityKey(userId);
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r < 0 THROW 51000, 'Account transaction busy.', 1;", token);
            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
    }

    private async Task Apply(string userId, long version, string state, DateTimeOffset pausedAt, LifecycleBarrierEntity? barrier, CancellationToken token)
    {
        if (barrier is null)
        {
            barrier = new() { UserId = userId };
            db.LifecycleBarriers.Add(barrier);
        }

        barrier.Version = version;
        barrier.State = state;
        var key = IdentityKey(userId);
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.IdentityKey == key, token);
        if (account is null)
            return;
        var rows = await db.Occurrences.Where(o => o.AccountId == account.Id).ToListAsync(token);
        var completed = await db.Completions.Where(o => o.AccountId == account.Id).ToListAsync(token);
        var undos = await db.Undos.Where(o => o.AccountId == account.Id).ToListAsync(token);
        var aggregate = new QuestAggregate(
            account.Zone,
            rows.Select(o => new Occurrence(o.Id, JsonSerializer.Deserialize<Quest>(o.QuestSnapshot)!, o.DueDate, o.Deadline, o.AcceptedAt, o.PlannedTime, o.ParentId, o.Lifecycle is null ? null : JsonSerializer.Deserialize<OccurrenceLifecycle>(o.Lifecycle))),
            completed.Select(c => new Completion(c.Id, c.OccurrenceId, c.RecordedAt, c.QuestSnapshot is null ? null : JsonSerializer.Deserialize<Quest>(c.QuestSnapshot))),
            undos.Select(u => new UndoEvent(u.Id, u.CompletionId, u.RecordedAt)),
            profile: account.Profile is null ? null : JsonSerializer.Deserialize<PlayerProfile>(account.Profile),
            schedule: account.Schedule is null ? null : JsonSerializer.Deserialize<ScheduleState>(account.Schedule));
        aggregate.Apply(new(Guid.NewGuid(), "pause"), pausedAt);
        account.Schedule = JsonSerializer.Serialize(aggregate.Schedule);
        foreach (var occurrence in aggregate.Occurrences)
        {
            var row = rows.SingleOrDefault(o => o.Id == occurrence.Id);
            if (row is null)
            {
                row = new() { AccountId = account.Id, Id = occurrence.Id, QuestSnapshot = JsonSerializer.Serialize(occurrence.Quest), AcceptedAt = occurrence.AcceptedAt, DueDate = occurrence.DueDate, Deadline = occurrence.Deadline, PlannedTime = occurrence.PlannedTime };
                db.Occurrences.Add(row);
            }

            row.Lifecycle = JsonSerializer.Serialize(occurrence.Lifecycle);
        }

        // Old device proofs cannot replay through a deletion/recovery transition.
        await db.SyncAnchors.Where(a => a.AccountId == account.Id).ExecuteDeleteAsync(token);
    }

    private async Task Purge(string userId, CancellationToken token)
    {
        var key = IdentityKey(userId);
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.IdentityKey == key, token);
        if (account is not null)
        {
            var id = account.Id;
            await db.Undos.Where(r => r.AccountId == id).ExecuteDeleteAsync(token);
            await db.Completions.Where(r => r.AccountId == id).ExecuteDeleteAsync(token);
            await db.Occurrences.Where(r => r.AccountId == id).ExecuteUpdateAsync(set => set.SetProperty(r => r.ParentId, (Guid?)null), token);
            await db.Occurrences.Where(r => r.AccountId == id).ExecuteDeleteAsync(token);
            await db.Operations.Where(r => r.AccountId == id).ExecuteDeleteAsync(token);
            await db.SyncAnchors.Where(r => r.AccountId == id).ExecuteDeleteAsync(token);
            await db.DefinitionRevisions.Where(r => r.AccountId == id).ExecuteDeleteAsync(token);
            await db.Definitions.Where(r => r.AccountId == id).ExecuteDeleteAsync(token);
            await db.Accounts.Where(r => r.Id == id).ExecuteDeleteAsync(token);
            db.Entry(account).State = EntityState.Detached;
        }

        await db.Audit.Where(a => a.Actor == userId).ExecuteDeleteAsync(token);
        await db.LifecycleBarriers.Where(b => b.UserId == userId).ExecuteDeleteAsync(token);
        foreach (var entry in db.ChangeTracker.Entries<LifecycleBarrierEntity>().Where(e => e.Entity.UserId == userId).ToArray())
            entry.State = EntityState.Detached;
        var ownerProperty = "\"UserId\":" + JsonSerializer.Serialize(userId);
        await db.Set<OutboxMessage>().Where(m => m.Type == typeof(LifecycleAck).AssemblyQualifiedName && m.Payload.Contains(ownerProperty)).ExecuteDeleteAsync(token);
    }
}
