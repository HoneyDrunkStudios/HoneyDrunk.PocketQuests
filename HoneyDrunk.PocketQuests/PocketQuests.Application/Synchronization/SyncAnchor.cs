namespace PocketQuests.Application.Synchronization;

/// <summary>A server-clock anchor bound to one authenticated product account and device process.</summary>
public record SyncAnchor(Guid Id, Guid DeviceId, Guid BootId, DateTimeOffset ServerUtc, DateTimeOffset DeviceUtc);
