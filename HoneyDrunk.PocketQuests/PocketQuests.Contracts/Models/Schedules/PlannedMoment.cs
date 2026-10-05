namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public PlannedMoment JSON contract, independent of storage and domain behavior.</summary>
public sealed record PlannedMoment(string Requested, string Resolved, DateTimeOffset Instant, bool Adjusted, bool Repeated);
