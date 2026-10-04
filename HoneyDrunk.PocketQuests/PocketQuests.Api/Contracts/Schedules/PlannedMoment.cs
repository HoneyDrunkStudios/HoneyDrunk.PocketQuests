using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>The public PlannedMoment JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record PlannedMoment(string Requested, string Resolved, DateTimeOffset Instant, bool Adjusted, bool Repeated);
