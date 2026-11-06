namespace PocketQuests.Contracts.Requests.Synchronization;

/// <summary>The device process and wall clock requesting a trusted offline baseline.</summary>
/// <param name="DeviceId">The device identifier used by recorded-clock evidence.</param>
/// <param name="BootId">The device boot identifier used by recorded-clock evidence.</param>
/// <param name="DeviceUtc">The device-reported UTC instant.</param>
public record AnchorRequest(Guid DeviceId, Guid BootId, DateTimeOffset DeviceUtc);
