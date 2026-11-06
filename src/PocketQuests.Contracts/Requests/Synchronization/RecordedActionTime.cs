namespace PocketQuests.Contracts.Requests.Synchronization;

/// <summary>The public RecordedActionTime JSON contract, independent of storage and domain behavior.</summary>
/// <param name="AnchorId">The recorded-clock anchor identifier.</param>
/// <param name="BootId">The device boot identifier used by recorded-clock evidence.</param>
/// <param name="Ordinal">The position within the ordered series.</param>
/// <param name="ElapsedMilliseconds">The device monotonic elapsed time in milliseconds.</param>
/// <param name="DeviceUtc">The device-reported UTC instant.</param>
public sealed record RecordedActionTime(Guid AnchorId, Guid BootId, long Ordinal, double ElapsedMilliseconds, DateTimeOffset DeviceUtc);
