namespace PocketQuests.Services.Synchronization.Mapping;

/// <summary>Explicit mappings for the synchronization HTTP contracts.</summary>
public static class SynchronizationContractMapping
{
    /// <summary>Maps RecordedActionTime explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Requests.Synchronization.RecordedActionTime ToModel(this PocketQuests.Domain.Models.Synchronization.RecordedActionTime value) => new(
        value.AnchorId,
        value.BootId,
        value.Ordinal,
        value.ElapsedMilliseconds,
        value.DeviceUtc);

    /// <summary>Maps RecordedActionTime explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Domain.Models.Synchronization.RecordedActionTime ToModel(this PocketQuests.Contracts.Requests.Synchronization.RecordedActionTime value) => new(
        value.AnchorId,
        value.BootId,
        value.Ordinal,
        value.ElapsedMilliseconds,
        value.DeviceUtc);

    /// <summary>Maps SyncAnchor explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Synchronization.SyncAnchor ToModel(this PocketQuests.Domain.Models.Synchronization.SyncAnchor value) => new(
        value.Id,
        value.DeviceId,
        value.BootId,
        value.ServerUtc,
        value.DeviceUtc,
        value.RecordedTimeFloor);
}
