namespace PocketQuests.Domain.Models.Synchronization;

/// <summary>Device evidence relative to an account-bound trusted server anchor; never arbitrary backdating.</summary>
/// <param name="AnchorId">The recorded-clock anchor identifier.</param>
/// <param name="BootId">The device boot identifier used by recorded-clock evidence.</param>
/// <param name="Ordinal">The position within the ordered series.</param>
/// <param name="ElapsedMilliseconds">The device monotonic elapsed time in milliseconds.</param>
/// <param name="DeviceUtc">The device-reported UTC instant.</param>
public record RecordedActionTime(Guid AnchorId, Guid BootId, long Ordinal, double ElapsedMilliseconds, DateTimeOffset DeviceUtc);
