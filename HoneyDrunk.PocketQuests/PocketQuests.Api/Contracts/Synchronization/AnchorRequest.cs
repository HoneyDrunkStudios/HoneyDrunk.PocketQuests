namespace PocketQuests.Api.Contracts.Synchronization;

/// <summary>The device process and wall clock requesting a trusted offline baseline.</summary>
public record AnchorRequest(Guid DeviceId, Guid BootId, DateTimeOffset DeviceUtc);
