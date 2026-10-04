using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Progress;

/// <summary>The public Balance JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record Balance(string Id, string Name, long Xp, int Level);
