namespace PocketQuests.Contracts.Models.Catalogs;

/// <summary>The public NamedItem JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Name">The human-readable name.</param>
public sealed record NamedItem(string Id, string Name);
