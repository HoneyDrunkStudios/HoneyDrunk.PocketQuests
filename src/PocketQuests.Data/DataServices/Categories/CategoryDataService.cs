using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.DataServices.Categories;

/// <summary>EF persistence and queries for Category.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CategoryDataService(AppDbContext context) : BaseDataService<CategoryEntity>(context), ICategoryDataService
{
    private IQueryable<CategoryEntity> Categories => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryEntity>> GetCatalog(CancellationToken cancellationToken = default) =>
        await Categories.OrderBy(row => row.SortOrder).ToListAsync(cancellationToken);
}
