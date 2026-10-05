using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Models.Synchronization;

namespace PocketQuests.Services.Synchronization.Mapping;

internal static class AnchorPersistenceMapping
{
    // The response preserves the submitted device offset; SQL stores its equivalent UTC instant.
    internal static SyncAnchor ToModel(this SyncAnchorEntity anchor, DateTimeOffset originalDeviceUtc) =>
        new(anchor.Id, anchor.DeviceId, anchor.BootId, anchor.ServerAt, originalDeviceUtc, anchor.RecordedTimeFloorAt);

    internal static SyncAnchorEntity Create(Guid id, AccountEntity account, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, DateTimeOffset floor) => new()
    {
        Id = id,
        AccountId = account.Id,
        DeviceId = deviceId,
        BootId = bootId,
        ServerAt = now.ToUniversalTime(),
        DeviceAt = deviceUtc.ToUniversalTime(),
        RecordedTimeFloorAt = floor,
        IssuedMutationVersion = account.MutationVersion,
    };
}
