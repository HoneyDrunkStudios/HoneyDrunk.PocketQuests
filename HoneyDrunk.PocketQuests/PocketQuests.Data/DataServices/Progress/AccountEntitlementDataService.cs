using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for AccountEntitlement.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountEntitlementDataService(AppDbContext context) : BaseDataService<AccountEntitlementEntity>(context), IAccountEntitlementDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountEntitlementEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
