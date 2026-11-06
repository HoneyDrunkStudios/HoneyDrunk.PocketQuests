namespace PocketQuests.Domain.Models.Catalogs;

/// <summary>A stable catalog identifier and its display name.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Name">The human-readable name.</param>
public record NamedItem(string Id, string Name);
