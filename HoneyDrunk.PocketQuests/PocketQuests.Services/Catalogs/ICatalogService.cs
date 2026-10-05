using PocketQuests.Contracts.Responses.Catalogs;

namespace PocketQuests.Services.Catalogs;

/// <summary>The canonical product catalog.</summary>
public interface ICatalogService
{
    /// <summary>Returns the existing catalog and reward rules.</summary>
    /// <returns>The public catalog.</returns>
    CatalogResponse Read();
}
