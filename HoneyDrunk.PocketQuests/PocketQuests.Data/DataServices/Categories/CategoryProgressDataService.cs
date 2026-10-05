using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.DataServices.Categories;

/// <summary>EF persistence and queries for CategoryProgress.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CategoryProgressDataService(AppDbContext context) : BaseDataService<CategoryProgressEntity>(context), ICategoryProgressDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryProgressEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
