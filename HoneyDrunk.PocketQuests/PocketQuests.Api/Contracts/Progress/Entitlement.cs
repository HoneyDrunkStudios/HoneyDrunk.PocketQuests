using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Progress;

/// <summary>The public Entitlement JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record Entitlement(string Id, string Kind, string Name, int Count, int RequiredCount, Rank RequiredRank, bool Earned);
