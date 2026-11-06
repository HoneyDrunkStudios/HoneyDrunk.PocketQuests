using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public ZonePreview JSON contract, independent of storage and domain behavior.</summary>
/// <param name="PreviousZone">The preceding account time zone.</param>
/// <param name="Zone">The applicable account time zone.</param>
/// <param name="Deadlines">The deadlines included in this result.</param>
public sealed record ZonePreview(string PreviousZone, string Zone, ImmutableArray<DeadlineChange> Deadlines);
