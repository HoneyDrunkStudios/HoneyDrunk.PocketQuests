namespace PocketQuests.Domain.Models.Synchronization;

/// <summary>A physical server-clock baseline and immutable action-ordering floor, bound to one account and device process.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="DeviceId">The device identifier used by recorded-clock evidence.</param>
/// <param name="BootId">The device boot identifier used by recorded-clock evidence.</param>
/// <param name="ServerUtc">The server UTC instant used by recorded-clock evidence.</param>
/// <param name="DeviceUtc">The device-reported UTC instant.</param>
/// <param name="RecordedTimeFloor">The lower bound for an accepted recorded instant.</param>
public record SyncAnchor(Guid Id, Guid DeviceId, Guid BootId, DateTimeOffset ServerUtc, DateTimeOffset DeviceUtc, DateTimeOffset? RecordedTimeFloor = null);
