using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Catalogs;

/// <summary>The public NamedItem JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record NamedItem(string Id, string Name);
