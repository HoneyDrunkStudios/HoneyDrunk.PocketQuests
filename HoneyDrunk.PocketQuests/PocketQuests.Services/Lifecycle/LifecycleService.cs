using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Lifecycle;
using PocketQuests.Data.DataServices.Synchronization;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Lifecycle.Mapping;
using PocketQuests.Services.Lifecycle.Validators;
using PocketQuests.Services.Quests;

namespace PocketQuests.Services.Lifecycle;

/// <summary>Commits verified Identity transitions, product pauses, fences and owned acknowledgments together.</summary>
/// <param name="data">Account lock, erasure and transaction ownership.</param>
/// <param name="barriers">Private lifecycle state persistence.</param>
/// <param name="markers">Minimal original erasure markers.</param>
/// <param name="messages">Owned acknowledgment persistence and retention.</param>
/// <param name="anchors">Recorded-time proof persistence.</param>
/// <param name="quests">Product mutation staging within the existing transaction.</param>
/// <param name="clock">Trusted host clock.</param>
public sealed class LifecycleService(IAccountDataService data, IAccountLifecycleStateDataService barriers, IErasureMarkerDataService markers, ILifecycleMessageDataService messages, ISyncAnchorDataService anchors, QuestService quests, TimeProvider clock) : ILifecycleService
{
    /// <inheritdoc />
    public Task ObserveActive(UserRecord user, CancellationToken token = default) => ObserveActive(user, clock.GetUtcNow(), token);

    /// <inheritdoc />
    public Task Receive(LifecycleIntent intent, string acknowledgmentQueue, CancellationToken token = default) => ReceiveLifecycle(intent, acknowledgmentQueue, clock.GetUtcNow(), token);

    /// <inheritdoc />
    public Task Prune(CancellationToken token = default) => PruneLifecycle(clock.GetUtcNow(), token);

    internal Task<bool> ObserveActive(UserRecord user, DateTimeOffset now, CancellationToken token = default)
    {
        var identity = LifecycleValidator.Identity(user.UserId);
        now = now.ToUniversalTime();
        LifecycleValidator.Active(user, now);
        return data.ExecuteInTransaction(Observe, token);

        async Task<bool> Observe(CancellationToken cancellationToken)
        {
            if (await markers.FindByIdAsync(user.UserId, cancellationToken) is not null)
                throw new UnauthorizedAccessException("This account was erased.");
            var seen = await barriers.ReadCurrent(user.UserId, cancellationToken);
            LifecycleValidator.ActiveVersion(user, seen);
            if (user.LifecycleVersion == (seen?.Version ?? 0))
                return false;
            await data.AcquireCommandLock(identity.Subject, cancellationToken);
            if (await markers.FindByIdAsync(user.UserId, cancellationToken) is not null)
                throw new UnauthorizedAccessException("This account was erased.");
            var barrier = await barriers.GetByIdentityUserId(user.UserId, cancellationToken);
            LifecycleValidator.ActiveVersion(user, barrier);
            if (user.LifecycleVersion > (barrier?.Version ?? 0))
                await Apply(identity, user.LifecycleVersion, IdentityProtocol.Active, now, user.DeletionPausedAt!.Value.ToUniversalTime(), barrier, now, cancellationToken);
            return true;
        }
    }

    internal async Task ReceiveLifecycle(LifecycleIntent intent, string queue, DateTimeOffset now, CancellationToken token = default)
    {
        var identity = LifecycleValidator.Identity(intent.UserId);
        now = now.ToUniversalTime();
        LifecycleValidator.Intent(intent, queue);
        if (now >= intent.ExpiresAt)
            return;
        LifecycleValidator.IntentClock(intent, now);
        await data.ExecuteInTransaction(ReceiveIntent, token);

        async Task<bool> ReceiveIntent(CancellationToken cancellationToken)
        {
            await data.AcquireCommandLock(identity.Subject, cancellationToken);
            var marker = await markers.FindByIdAsync(identity.Subject, cancellationToken);
            var barrier = await barriers.GetByIdentityUserId(identity.Subject, cancellationToken);
            if (marker is null && intent.Version > (barrier?.Version ?? 0))
            {
                if (intent.State == IdentityProtocol.Erasing)
                {
                    var oldAbsentRetry = intent.EffectiveAt <= now.AddDays(-35) && barrier is null
                        && await data.GetByIdentityUserId(identity.Subject, cancellationToken) is null;
                    if (!oldAbsentRetry)
                        await Purge(identity.Subject, now, cancellationToken);
                }
                else
                {
                    await Apply(identity, intent.Version, intent.State, intent.EffectiveAt.ToUniversalTime(), intent.PausedAt.ToUniversalTime(), barrier, now, cancellationToken);
                }
            }

            await LifecycleAcknowledgments.Stage(data, messages, intent, queue, now, cancellationToken);
            return true;
        }
    }

    internal Task<bool> ReapplyErasure(string userId, DateTimeOffset originalErasedAt, DateTimeOffset now, CancellationToken token = default)
    {
        LifecycleValidator.Erasure(originalErasedAt, now);
        var identity = LifecycleValidator.Identity(userId);
        return data.ExecuteInTransaction(Restore, token);

        async Task<bool> Restore(CancellationToken cancellationToken)
        {
            await data.AcquireCommandLock(identity.Subject, cancellationToken);
            await Purge(identity.Subject, originalErasedAt, cancellationToken);
            return true;
        }
    }

    internal Task<bool> PruneLifecycle(DateTimeOffset now, CancellationToken token = default)
    {
        return data.ExecuteInTransaction(PruneOwned, token);

        async Task<bool> PruneOwned(CancellationToken cancellationToken)
        {
            await messages.DeleteDeliveredOrExpired(now.ToUniversalTime(), cancellationToken);
            await markers.DeleteExpired(now.ToUniversalTime().AddDays(-35), cancellationToken);
            return true;
        }
    }

    private async Task Purge(string userId, DateTimeOffset originalErasedAt, CancellationToken token)
    {
        await data.DeleteOwned(userId, token);
        if (await markers.FindByIdAsync(userId, token) is null)
            await markers.AddAsync(LifecycleMapping.ToMarker(userId, originalErasedAt), token);
    }

    private async Task Apply(AccountIdentity identity, long version, string state, DateTimeOffset effectiveAt, DateTimeOffset pausedAt, AccountLifecycleStateEntity? barrier, DateTimeOffset now, CancellationToken token)
    {
        var account = await data.GetByIdentityUserId(identity.Subject, token);
        if (account is not null)
            await quests.StageLifecyclePause(identity, account, pausedAt, now, token);
        if (barrier is null)
        {
            barrier = LifecycleMapping.Create(identity.Subject, now);
            await barriers.AddAsync(barrier, token);
        }

        barrier.ApplyTo(account?.Id, version, state, effectiveAt, pausedAt, now);
        if (account is not null)
        {
            foreach (var anchor in await anchors.GetByAccountId(account.Id, token))
            {
                if (anchor.InvalidatedAt is null)
                    anchor.Invalidate(now);
            }
        }
    }
}
