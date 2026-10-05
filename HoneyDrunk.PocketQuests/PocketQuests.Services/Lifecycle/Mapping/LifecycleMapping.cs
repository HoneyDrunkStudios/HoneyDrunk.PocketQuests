using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Services.Lifecycle.Mapping;

internal static class LifecycleMapping
{
    internal static AccountLifecycleStateEntity Create(string userId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        IdentityUserId = userId,
        CreatedAt = now,
    };

    internal static void ApplyTo(this AccountLifecycleStateEntity barrier, Guid? accountId, long version, string state, DateTimeOffset effectiveAt, DateTimeOffset pausedAt, DateTimeOffset now)
    {
        barrier.AccountId = accountId;
        barrier.Version = version;
        barrier.StateCode = state;
        barrier.EffectiveAt = effectiveAt;
        barrier.PausedAt = pausedAt;
        barrier.ModifiedAt = barrier.ModifiedAt > now ? barrier.ModifiedAt : now;
    }

    internal static void Invalidate(this SyncAnchorEntity anchor, DateTimeOffset now)
    {
        anchor.InvalidatedAt = now;
        anchor.ModifiedAt = anchor.ModifiedAt > now ? anchor.ModifiedAt : now;
    }

    internal static ErasureMarkerEntity ToMarker(string userId, DateTimeOffset originalErasedAt) => new()
    {
        Id = userId,
        CreatedAt = originalErasedAt.ToUniversalTime(),
    };
}
