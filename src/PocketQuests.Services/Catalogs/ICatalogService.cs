using PocketQuests.Contracts.Responses.Catalogs;

namespace PocketQuests.Services.Catalogs;

/// <summary>The canonical product catalog.</summary>
public interface ICatalogService
{
    /// <summary>Returns the existing catalog and reward rules.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>The public catalog.</returns>
    Task<CatalogResponse> Read(CancellationToken token = default);
}
