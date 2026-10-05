using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for AccountInterest.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountInterestDataService(AppDbContext context) : BaseDataService<AccountInterestEntity>(context), IAccountInterestDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountInterestEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
