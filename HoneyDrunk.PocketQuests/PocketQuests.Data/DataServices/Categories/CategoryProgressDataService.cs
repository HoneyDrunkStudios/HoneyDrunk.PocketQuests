using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.DataServices.Categories;

/// <summary>EF persistence and queries for CategoryProgress.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CategoryProgressDataService(AppDbContext context) : BaseDataService<CategoryProgressEntity>(context), ICategoryProgressDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryProgressEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
