namespace PocketQuests.Contracts.Requests.Synchronization;

/// <summary>The public RecordedActionTime JSON contract, independent of storage and domain behavior.</summary>
public sealed record RecordedActionTime(Guid AnchorId, Guid BootId, long Ordinal, double ElapsedMilliseconds, DateTimeOffset DeviceUtc);
