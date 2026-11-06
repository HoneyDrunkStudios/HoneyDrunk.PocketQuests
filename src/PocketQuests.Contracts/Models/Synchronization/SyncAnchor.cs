namespace PocketQuests.Contracts.Models.Synchronization;

/// <summary>The public SyncAnchor JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="DeviceId">The device identifier used by recorded-clock evidence.</param>
/// <param name="BootId">The device boot identifier used by recorded-clock evidence.</param>
/// <param name="ServerUtc">The server UTC instant used by recorded-clock evidence.</param>
/// <param name="DeviceUtc">The device-reported UTC instant.</param>
/// <param name="RecordedTimeFloor">The lower bound for an accepted recorded instant.</param>
public sealed record SyncAnchor(Guid Id, Guid DeviceId, Guid BootId, DateTimeOffset ServerUtc, DateTimeOffset DeviceUtc, DateTimeOffset? RecordedTimeFloor);
