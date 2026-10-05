using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for XpBalance.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class XpBalanceDataService(AppDbContext context) : BaseDataService<XpBalanceEntity>(context), IXpBalanceDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<XpBalanceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
