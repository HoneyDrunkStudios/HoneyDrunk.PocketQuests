using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using PocketQuests.Data;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Lifecycle;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Services.Accounts;
using PocketQuests.Domain.Services.Quests;
using PocketQuests.Domain.Services.Synchronization;

namespace PocketQuests.Domain.Services.Lifecycle;

/// <summary>Retains AccountLifecycleState ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="db">Scoped QuestDbContext dependency.</param>
/// <param name="accountData">Scoped IAccountDataService dependency.</param>
/// <param name="markerData">Scoped IErasureMarkerDataService dependency.</param>
/// <param name="erasureService">Scoped IErasureMarkerService dependency.</param>
/// <param name="messageService">Scoped ILifecycleMessageService dependency.</param>
/// <param name="anchorService">Scoped ISyncAnchorService dependency.</param>
/// <param name="quests">Scoped IQuestService dependency.</param>
/// <param name="clock">Scoped TimeProvider dependency.</param>
public sealed class AccountLifecycleStateService(IAccountLifecycleStateDataService data, QuestDbContext db, IAccountDataService accountData, IErasureMarkerDataService markerData, IErasureMarkerService erasureService, ILifecycleMessageService messageService, ISyncAnchorService anchorService, IQuestService quests, TimeProvider clock) : IAccountLifecycleStateService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<AccountLifecycleStateEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<AccountLifecycleStateEntity> SaveAsync(AccountLifecycleStateEntity value, CancellationToken cancellationToken = default)
    {
        var current = await data.FindByIdAsync(value.Id, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.Id != current.Id
            || original.IdentityUserId != current.IdentityUserId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Version < original.Version)
            throw new InvalidOperationException("A lifecycle fence cannot move backwards.");
        if (current.Id != value.Id
            || current.IdentityUserId != value.IdentityUserId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (value.Version < current.Version)
            throw new InvalidOperationException("A lifecycle fence cannot move backwards.");

        current.AccountId = value.AccountId;
        current.Version = value.Version;
        current.StateCode = value.StateCode;
        current.EffectiveAt = value.EffectiveAt;
        current.PausedAt = value.PausedAt;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public Task ObserveActive(UserRecord user, CancellationToken token = default) => ObserveActive(user, clock.GetUtcNow(), token);

    /// <inheritdoc />
    public Task Receive(LifecycleIntent intent, string acknowledgmentQueue, CancellationToken token = default) => ReceiveLifecycle(intent, acknowledgmentQueue, clock.GetUtcNow(), token);

    /// <inheritdoc />
    public Task Prune(CancellationToken token = default) => PruneLifecycle(clock.GetUtcNow(), token);

    /// <summary>Fences stale resolution and durably freezes recovered progress until the existing explicit resume command.</summary>
    /// <param name="user">Current authoritative Identity response, never a public request body.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion once any new lifecycle version is durable.</returns>
    public async Task ObserveActive(UserRecord user, DateTimeOffset now, CancellationToken token = default)
    {
        var identity = LifecycleIdentity(user.UserId);
        IdentityValidation.RequireCanonical(identity);
        now = now.ToUniversalTime();
        if (user.State != IdentityProtocol.Active || user.LifecycleVersion < 0
            || (user.LifecycleVersion > 0 && user.DeletionPausedAt is null)
            || user.DeletionPausedAt > now.AddSeconds(30))
            throw new UnauthorizedAccessException("Account is inactive or its lifecycle is incomplete.");

        // An unchanged authoritative resolution does not take the exclusive account lock.
        // The subsequent product read/command independently fences a concurrent transition.
        BeginOperation();
        if (await markerData.FindByIdAsync(user.UserId, token) is not null)
            throw new UnauthorizedAccessException("This account was erased.");
        var seen = await data.GetByIdentityUserIdAsync(user.UserId, token);
        RequireActiveVersion(user, seen);
        if (user.LifecycleVersion == (seen?.Version ?? 0))
            return;
        db.ChangeTracker.Clear();

        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await accountData.AcquireCommandLockAsync(identity.Subject, token);
        if (await markerData.FindByIdAsync(user.UserId, token) is not null)
            throw new UnauthorizedAccessException("This account was erased.");
        var barrier = await data.GetByIdentityUserIdAsync(user.UserId, token);
        RequireActiveVersion(user, barrier);
        if (user.LifecycleVersion > (barrier?.Version ?? 0))
            await ApplyLifecycle(identity, user.LifecycleVersion, IdentityProtocol.Active, now, user.DeletionPausedAt!.Value.ToUniversalTime(), barrier, now, token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
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
        IdentityValidation.RequireCanonical(identity);
        now = now.ToUniversalTime();
        if (intent.Consumer != IdentityProtocol.ConsumerId || intent.State is not (IdentityProtocol.Active or IdentityProtocol.Inactive or IdentityProtocol.Erasing)
            || intent.Version <= 0 || intent.Acknowledgment?.Length != 64 || string.IsNullOrWhiteSpace(acknowledgmentQueue))
            throw new UnauthorizedAccessException("Invalid lifecycle instruction.");
        if (now >= intent.ExpiresAt)
            return;
        if (intent.EffectiveAt > now.AddSeconds(30) || intent.PausedAt > intent.EffectiveAt || intent.ExpiresAt > now.AddHours(1).AddSeconds(30))
            throw new ArgumentException("Invalid lifecycle clock.");
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await accountData.AcquireCommandLockAsync(identity.Subject, token);
        var marker = await markerData.FindByIdAsync(identity.Subject, token);
        var barrier = await data.GetByIdentityUserIdAsync(identity.Subject, token);
        if (marker is null && intent.Version > (barrier?.Version ?? 0))
        {
            if (intent.State == IdentityProtocol.Erasing)
            {
                // After the marker's retention horizon, a renewed old capability for an
                // already absent account must not invent a new erasure time/retention window.
                // If owned account/fence data still exists, this is actual erasure now.
                var oldAbsentRetry = intent.EffectiveAt <= now.AddDays(-35) && barrier is null
                    && await accountData.GetByIdentityUserIdAsync(identity.Subject, token) is null;
                if (!oldAbsentRetry)
                    await erasureService.PurgeAsync(identity.Subject, now, now, token);
            }
            else
            {
                await ApplyLifecycle(identity, intent.Version, intent.State, intent.EffectiveAt.ToUniversalTime(), intent.PausedAt.ToUniversalTime(), barrier, now, token);
            }
        }

        await messageService.AcknowledgeAsync(intent, acknowledgmentQueue, now, token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
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
        IdentityValidation.RequireCanonical(identity);
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await accountData.AcquireCommandLockAsync(identity.Subject, token);
        await erasureService.PurgeAsync(identity.Subject, originalErasedAt.ToUniversalTime(), now.ToUniversalTime(), token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    /// <summary>Removes only owned dispatched/expired acknowledgments and markers at their original thirty-five-day boundary.</summary>
    /// <param name="now">Retention host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the atomic owned-envelope and marker cleanup.</returns>
    public async Task PruneLifecycle(DateTimeOffset now, CancellationToken token = default)
    {
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await messageService.PruneAsync(now.ToUniversalTime(), token);
        await erasureService.PruneAsync(now.ToUniversalTime(), token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private static AccountIdentity LifecycleIdentity(string userId) => new("honeydrunk-identity", userId);

    private static void RequireActiveVersion(UserRecord user, AccountLifecycleStateEntity? barrier)
    {
        if (barrier is not null && (barrier.Version > user.LifecycleVersion || (barrier.Version == user.LifecycleVersion && barrier.StateCode != IdentityProtocol.Active)))
            throw new UnauthorizedAccessException("Account lifecycle changed; sign in again.");
    }

    private void BeginOperation()
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Lifecycle operations require their own transaction.");
        db.ChangeTracker.Clear();
    }

    private async Task ApplyLifecycle(AccountIdentity identity, long version, string state, DateTimeOffset effectiveAt, DateTimeOffset pausedAt, AccountLifecycleStateEntity? barrier, DateTimeOffset now, CancellationToken token)
    {
        var account = await accountData.GetByIdentityUserIdAsync(identity.Subject, token);
        if (account is not null)
            await quests.StageLifecyclePauseAsync(identity, account, pausedAt, now, token);
        await SaveAsync(
            new AccountLifecycleStateEntity
            {
                Id = barrier?.Id ?? Guid.NewGuid(),
                IdentityUserId = identity.Subject,
                AccountId = account?.Id,
                Version = version,
                StateCode = state,
                EffectiveAt = effectiveAt,
                PausedAt = pausedAt,
                CreatedAt = barrier?.CreatedAt ?? now,
                ModifiedAt = now
            },
            token);
        if (account is not null)
        {
            foreach (var anchor in await anchorService.GetByAccountIdAsync(account.Id, token))
            {
                if (anchor.InvalidatedAt is not null)
                    continue;
                anchor.InvalidatedAt = now;
                anchor.ModifiedAt = anchor.ModifiedAt > now ? anchor.ModifiedAt : now;
                await anchorService.SaveAsync(account.Id, anchor, token);
            }
        }
    }
}
