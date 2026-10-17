namespace PocketQuests.Domain.Models.Synchronization;

/// <summary>Device evidence relative to an account-bound trusted server anchor; never arbitrary backdating.</summary>
public record RecordedActionTime(Guid AnchorId, Guid BootId, long Ordinal, double ElapsedMilliseconds, DateTimeOffset DeviceUtc);
