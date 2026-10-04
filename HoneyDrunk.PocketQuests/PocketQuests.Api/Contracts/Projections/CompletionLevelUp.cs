using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Projections;

/// <summary>The public CompletionLevelUp JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record CompletionLevelUp(string Track, string TrackId, string Name, int From, int To);
