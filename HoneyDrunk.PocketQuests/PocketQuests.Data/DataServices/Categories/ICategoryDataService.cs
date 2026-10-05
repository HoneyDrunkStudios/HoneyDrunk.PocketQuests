using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.DataServices.Categories;

/// <summary>Persistence operations and entity-specific queries for Category.</summary>
public interface ICategoryDataService : IBaseDataService<CategoryEntity>
{
    /// <summary>Gets the versioned public catalog.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<CategoryEntity>> GetCatalog(CancellationToken cancellationToken = default);
}
