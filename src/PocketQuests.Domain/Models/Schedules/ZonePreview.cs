using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Schedules;

/// <summary>Reviewable active deadline changes; final history is excluded.</summary>
/// <param name="PreviousZone">The preceding account time zone.</param>
/// <param name="Zone">The applicable account time zone.</param>
/// <param name="Deadlines">The deadlines included in this result.</param>
public record ZonePreview(string PreviousZone, string Zone, ImmutableArray<DeadlineChange> Deadlines);
