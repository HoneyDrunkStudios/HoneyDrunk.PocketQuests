using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>The public ZoneChange JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record ZoneChange(string From, string To, DateTimeOffset At);
