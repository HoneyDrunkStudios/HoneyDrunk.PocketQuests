using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>The public PauseWindow JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record PauseWindow(string CategoryId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
