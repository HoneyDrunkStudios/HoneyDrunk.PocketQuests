namespace PocketQuests.Api.Contracts.Catalogs;

/// <summary>Explicit mappings for the catalogs HTTP contracts.</summary>
public static class CatalogsContractMapping
{
    /// <summary>Maps NamedItem explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Catalogs.NamedItem ToContract(this PocketQuests.Domain.Models.Catalogs.NamedItem value) => new(
        value.Id,
        value.Name);
}
