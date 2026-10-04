using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Synchronization;

/// <summary>The public SyncAnchor JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record SyncAnchor(Guid Id, Guid DeviceId, Guid BootId, DateTimeOffset ServerUtc, DateTimeOffset DeviceUtc, DateTimeOffset? RecordedTimeFloor);
