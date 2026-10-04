using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Profiles;

/// <summary>The public CustomSkill JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record CustomSkill(string Id, string Name, int Revision, bool Archived);
