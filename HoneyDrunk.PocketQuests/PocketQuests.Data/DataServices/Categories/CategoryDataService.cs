using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.DataServices.Categories;

/// <summary>EF persistence and queries for Category.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CategoryDataService(AppDbContext context) : BaseDataService<CategoryEntity>(context), ICategoryDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        await DbSet.OrderBy(row => row.SortOrder).ToListAsync(cancellationToken);
}
