using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Domain.Services.Categories;

/// <summary>Business access and invariants for Category.</summary>
public interface ICategoryService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<CategoryEntity>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
