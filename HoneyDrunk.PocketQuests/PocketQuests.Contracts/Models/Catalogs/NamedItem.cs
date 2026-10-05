namespace PocketQuests.Contracts.Models.Catalogs;

/// <summary>The public NamedItem JSON contract, independent of storage and domain behavior.</summary>
public sealed record NamedItem(string Id, string Name);
