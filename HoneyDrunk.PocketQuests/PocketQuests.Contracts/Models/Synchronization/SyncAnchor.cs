namespace PocketQuests.Contracts.Models.Synchronization;

/// <summary>The public SyncAnchor JSON contract, independent of storage and domain behavior.</summary>
public sealed record SyncAnchor(Guid Id, Guid DeviceId, Guid BootId, DateTimeOffset ServerUtc, DateTimeOffset DeviceUtc, DateTimeOffset? RecordedTimeFloor);
