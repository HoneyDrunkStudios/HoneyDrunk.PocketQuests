using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.DataServices.Categories;

/// <summary>Persistence operations and entity-specific queries for CategoryProgress.</summary>
public interface ICategoryProgressDataService : IBaseDataService<CategoryProgressEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<CategoryProgressEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
