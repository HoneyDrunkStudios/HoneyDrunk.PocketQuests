using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Progress;

/// <summary>The public Streak JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record Streak(string CategoryId, int Days, bool QualifiedToday, int Rate);
