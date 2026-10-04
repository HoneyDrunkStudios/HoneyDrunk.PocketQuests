namespace PocketQuests.Data.Entities.Categories;

/// <summary>One row is one of the ten canonical life categories. Classification: Public. History: reference; Retain stable codes indefinitely; source control owns reference history. Not erased with accounts.</summary>
public sealed class CategoryEntity
{
    /// <summary>Gets or sets stable public catalog code; preserve existing IDs and ordinal allocation tie-breaking.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets current public catalog display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets version of the reviewed catalog that supplied this row.</summary>
    public string CatalogVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets positive canonical display order; not an independently chosen progression priority.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }
}
