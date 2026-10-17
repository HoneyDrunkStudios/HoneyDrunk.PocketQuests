namespace PocketQuests.Domain.Models.Synchronization;

/// <summary>A physical server-clock baseline and immutable action-ordering floor, bound to one account and device process.</summary>
public record SyncAnchor(Guid Id, Guid DeviceId, Guid BootId, DateTimeOffset ServerUtc, DateTimeOffset DeviceUtc, DateTimeOffset? RecordedTimeFloor = null);
