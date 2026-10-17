using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public ZonePreview JSON contract, independent of storage and domain behavior.</summary>
public sealed record ZonePreview(string PreviousZone, string Zone, ImmutableArray<DeadlineChange> Deadlines);
