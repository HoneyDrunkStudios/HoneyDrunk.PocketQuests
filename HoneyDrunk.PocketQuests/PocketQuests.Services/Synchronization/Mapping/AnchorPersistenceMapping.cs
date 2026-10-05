using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Services.Synchronization.Mapping;

internal static class AnchorPersistenceMapping
{
    internal static SyncAnchorEntity Create(AccountEntity account, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, DateTimeOffset floor) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = account.Id,
        DeviceId = deviceId,
        BootId = bootId,
        ServerAt = now.ToUniversalTime(),
        DeviceAt = deviceUtc.ToUniversalTime(),
        RecordedTimeFloorAt = floor,
        IssuedMutationVersion = account.MutationVersion,
        CreatedAt = now.ToUniversalTime(),
        ModifiedAt = now.ToUniversalTime(),
    };
}
