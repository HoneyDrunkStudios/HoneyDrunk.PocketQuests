using HoneyDrunk.Data.Outbox;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Commands;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Verified Identity transitions share the command lock, while acknowledgment ownership and erasure stay explicit.</summary>
public sealed partial class RelationalQuestCommands
{
    /// <summary>Fences stale resolution and durably freezes recovered progress until the existing explicit resume command.</summary>
    /// <param name="user">Current authoritative Identity response, never a public request body.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion once any new lifecycle version is durable.</returns>
    public async Task ObserveActive(UserRecord user, DateTimeOffset now, CancellationToken token = default)
    {
        var identity = LifecycleIdentity(user.UserId);
        ValidateIdentity(identity);
        now = now.ToUniversalTime();
        if (user.State != IdentityProtocol.Active || user.LifecycleVersion < 0
            || (user.LifecycleVersion > 0 && user.DeletionPausedAt is null)
            || user.DeletionPausedAt > now.AddSeconds(30))
            throw new UnauthorizedAccessException("Account is inactive or its lifecycle is incomplete.");

        // An unchanged authoritative resolution does not take the exclusive account lock.
        // The subsequent product read/command independently fences a concurrent transition.
        await using (var read = new RelationalQuestReadContext(new DbContextOptionsBuilder<RelationalQuestReadContext>().UseSqlServer(connectionString).Options))
        {
            if (await read.Set<ErasureMarkerEntity>().AnyAsync(m => m.Id == user.UserId, token))
                throw new UnauthorizedAccessException("This account was erased.");
            var seen = await read.Set<AccountLifecycleStateEntity>().SingleOrDefaultAsync(b => b.IdentityUserId == user.UserId, token);
            RequireActiveVersion(user, seen);
            if (user.LifecycleVersion == (seen?.Version ?? 0))
                return;
        }

        await using var session = await Open(identity, token, lifecycle: true);
        if (await session.Context.Set<ErasureMarkerEntity>().AnyAsync(m => m.Id == user.UserId, token))
            throw new UnauthorizedAccessException("This account was erased.");
        var barrier = await session.Context.Set<AccountLifecycleStateEntity>().SingleOrDefaultAsync(b => b.IdentityUserId == user.UserId, token);
        RequireActiveVersion(user, barrier);
        if (user.LifecycleVersion > (barrier?.Version ?? 0))
            await ApplyLifecycle(session, identity, user.LifecycleVersion, IdentityProtocol.Active, now, user.DeletionPausedAt!.Value.ToUniversalTime(), barrier, now, token);
        await session.Transaction.CommitAsync(token);
    }

    /// <summary>Consumes a verified private Identity instruction and stages its capability acknowledgment atomically.</summary>
    /// <param name="intent">Instruction from the authorized private consumer endpoint.</param>
    /// <param name="acknowledgmentQueue">Trusted host configuration.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion only after transition and outbox ownership commit.</returns>
    public async Task ReceiveLifecycle(LifecycleIntent intent, string acknowledgmentQueue, DateTimeOffset now, CancellationToken token = default)
    {
        var identity = LifecycleIdentity(intent.UserId);
        ValidateIdentity(identity);
        now = now.ToUniversalTime();
        if (intent.Consumer != IdentityProtocol.ConsumerId || intent.State is not (IdentityProtocol.Active or IdentityProtocol.Inactive or IdentityProtocol.Erasing)
            || intent.Version <= 0 || intent.Acknowledgment?.Length != 64 || string.IsNullOrWhiteSpace(acknowledgmentQueue))
            throw new UnauthorizedAccessException("Invalid lifecycle instruction.");
        if (now >= intent.ExpiresAt)
            return;
        if (intent.EffectiveAt > now.AddSeconds(30) || intent.PausedAt > intent.EffectiveAt || intent.ExpiresAt > now.AddHours(1).AddSeconds(30))
            throw new ArgumentException("Invalid lifecycle clock.");
        await using var session = await Open(identity, token, lifecycle: true);
        var marker = await session.Context.Set<ErasureMarkerEntity>().SingleOrDefaultAsync(m => m.Id == identity.Subject, token);
        var barrier = await session.Context.Set<AccountLifecycleStateEntity>().SingleOrDefaultAsync(b => b.IdentityUserId == identity.Subject, token);
        if (marker is null && intent.Version > (barrier?.Version ?? 0))
        {
            if (intent.State == IdentityProtocol.Erasing)
            {
                // After the marker's retention horizon, a renewed old capability for an
                // already absent account must not invent a new erasure time/retention window.
                // If owned account/fence data still exists, this is actual erasure now.
                var oldAbsentRetry = intent.EffectiveAt <= now.AddDays(-35) && barrier is null
                    && !await session.Context.Set<AccountEntity>().AnyAsync(a => a.IdentityUserId == identity.Subject, token);
                if (!oldAbsentRetry)
                    await Purge(session, identity, now, now, token);
            }
            else
            {
                await ApplyLifecycle(session, identity, intent.Version, intent.State, intent.EffectiveAt.ToUniversalTime(), intent.PausedAt.ToUniversalTime(), barrier, now, token);
            }
        }

        var acknowledgment = new LifecycleAck(identity.Subject, intent.Version, IdentityProtocol.ConsumerId, intent.Acknowledgment);
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{identity.Subject}/{intent.Version}/{intent.Acknowledgment}")).AsSpan(0, 16));
        await using var sql = session.Procedure("pocketquests.StageLifecycleAcknowledgment");
        Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
        Add(sql, "@Version", SqlDbType.BigInt, intent.Version);
        Add(sql, "@Id", SqlDbType.UniqueIdentifier, id);
        Add(sql, "@Type", SqlDbType.NVarChar, typeof(LifecycleAck).AssemblyQualifiedName!, 512);
        Add(sql, "@Payload", SqlDbType.NVarChar, JsonSerializer.Serialize(acknowledgment), -1);
        Add(sql, "@Headers", SqlDbType.NVarChar, JsonSerializer.Serialize(new Dictionary<string, string> { [OutboxHeaderNames.Destination] = acknowledgmentQueue }), -1);
        Add(sql, "@ExpiresAt", SqlDbType.DateTimeOffset, (intent.ExpiresAt < now.AddHours(1) ? intent.ExpiresAt : now.AddHours(1)).ToUniversalTime());
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now);
        await sql.ExecuteNonQueryAsync(token);
        await session.Transaction.CommitAsync(token);
    }

    /// <summary>Reapplies external verified erasure evidence while the restored database remains closed to product traffic.</summary>
    /// <param name="identityUserId">Canonical marker identifier from the verified external source.</param>
    /// <param name="originalErasedAt">Original verified live-erasure time; never the restore/retry clock.</param>
    /// <param name="now">Restore host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion after scoped purge and unchanged original marker insertion.</returns>
    public async Task ReapplyErasure(string identityUserId, DateTimeOffset originalErasedAt, DateTimeOffset now, CancellationToken token = default)
    {
        if (originalErasedAt > now || originalErasedAt <= now.AddDays(-35))
            throw new ArgumentException("A current verified marker is required.");
        var identity = LifecycleIdentity(identityUserId);
        await using var session = await Open(identity, token, lifecycle: true);
        await Purge(session, identity, originalErasedAt.ToUniversalTime(), now.ToUniversalTime(), token);
        await session.Transaction.CommitAsync(token);
    }

    /// <summary>Removes only owned dispatched/expired acknowledgments and markers at their original thirty-five-day boundary.</summary>
    /// <param name="now">Retention host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the atomic owned-envelope and marker cleanup.</returns>
    public async Task PruneLifecycle(DateTimeOffset now, CancellationToken token = default)
    {
        await using var session = await OpenSession(token);
        await using var sql = session.Procedure("pocketquests.PruneLifecycle");
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now.ToUniversalTime());
        Add(sql, "@DispatchedStatus", SqlDbType.Int, (int)OutboxMessageStatus.Dispatched);
        await sql.ExecuteNonQueryAsync(token);
        await session.Transaction.CommitAsync(token);
    }

    private static AccountIdentity LifecycleIdentity(string userId) => new("honeydrunk-identity", userId);

    private static void RequireActiveVersion(UserRecord user, AccountLifecycleStateEntity? barrier)
    {
        if (barrier is not null && (barrier.Version > user.LifecycleVersion || (barrier.Version == user.LifecycleVersion && barrier.StateCode != IdentityProtocol.Active)))
            throw new UnauthorizedAccessException("Account lifecycle changed; sign in again.");
    }

    private static async Task ApplyLifecycle(Session session, AccountIdentity identity, long version, string state, DateTimeOffset effectiveAt, DateTimeOffset pausedAt, AccountLifecycleStateEntity? barrier, DateTimeOffset now, CancellationToken token)
    {
        var account = await session.Context.Set<AccountEntity>().SingleOrDefaultAsync(a => a.IdentityUserId == identity.Subject, token);
        if (account is not null)
        {
            var aggregate = await Load(session, account, account.MutationVersion, token);
            var command = new QuestCommand(Guid.NewGuid(), "$lifecycle-pause");
            var pending = aggregate.Apply(new(command.OperationId, QuestActions.Pause), pausedAt, 0);
            var at = Max(Max(now, account.LastRecordedAt), pausedAt);
            await CommitMutation(session, identity, account, command, CommandDigest.Compute(command), aggregate, aggregate.Project(at), pausedAt, at, at, now, token, reconciliationLimit: 0, actionReconciliationLimit: 0, internalTransition: true, hasPending: pending.HasMore);
        }

        await using var sql = session.Procedure("pocketquests.SetLifecycleState");
        Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
        Add(sql, "@ExpectedVersion", SqlDbType.BigInt, barrier?.Version ?? 0);
        Add(sql, "@Version", SqlDbType.BigInt, version);
        Add(sql, "@State", SqlDbType.VarChar, state, 8);
        Add(sql, "@EffectiveAt", SqlDbType.DateTimeOffset, effectiveAt);
        Add(sql, "@PausedAt", SqlDbType.DateTimeOffset, pausedAt);
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now);
        await sql.ExecuteNonQueryAsync(token);
    }

    private static async Task Purge(Session session, AccountIdentity identity, DateTimeOffset originalErasedAt, DateTimeOffset now, CancellationToken token)
    {
        await using var sql = session.Procedure("pocketquests.PurgeAccount");
        Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
        Add(sql, "@MarkerCreatedAt", SqlDbType.DateTimeOffset, originalErasedAt);
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now);
        await sql.ExecuteNonQueryAsync(token);
    }
}
