using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>The public ZonePreview JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record ZonePreview(string PreviousZone, string Zone, ImmutableArray<DeadlineChange> Deadlines);
