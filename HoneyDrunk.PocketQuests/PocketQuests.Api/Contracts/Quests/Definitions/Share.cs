using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Quests.Definitions;

/// <summary>The public Share JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record Share(string Id, int BasisPoints);
