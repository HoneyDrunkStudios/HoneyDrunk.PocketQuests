using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Services.Lifecycle.Mapping;

internal static class LifecycleMapping
{
    internal static AccountLifecycleStateEntity Create(Guid id, string userId) => new()
    {
        Id = id,
        IdentityUserId = userId,
    };

    internal static void ApplyTo(this AccountLifecycleStateEntity barrier, Guid? accountId, long version, string state, DateTimeOffset effectiveAt, DateTimeOffset pausedAt)
    {
        barrier.AccountId = accountId;
        barrier.Version = version;
        barrier.StateCode = state;
        barrier.EffectiveAt = effectiveAt;
        barrier.PausedAt = pausedAt;
    }

    internal static void Invalidate(this SyncAnchorEntity anchor, DateTimeOffset now)
    {
        anchor.InvalidatedAt = now;
    }

    internal static ErasureMarkerEntity ToMarker(string userId, DateTimeOffset originalErasedAt) => new()
    {
        Id = userId,
        CreatedAt = originalErasedAt.ToUniversalTime(),
    };
}
