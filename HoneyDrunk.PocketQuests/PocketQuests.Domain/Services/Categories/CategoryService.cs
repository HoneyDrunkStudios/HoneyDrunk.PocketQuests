using PocketQuests.Data.DataServices.Categories;
using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Domain.Services.Categories;

/// <summary>Retains Category ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class CategoryService(ICategoryDataService data) : ICategoryService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<CategoryEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        data.GetCatalogAsync(cancellationToken);
}
