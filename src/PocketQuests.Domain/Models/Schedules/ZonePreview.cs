using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Schedules;

/// <summary>Reviewable active deadline changes; final history is excluded.</summary>
public record ZonePreview(string PreviousZone, string Zone, ImmutableArray<DeadlineChange> Deadlines);
