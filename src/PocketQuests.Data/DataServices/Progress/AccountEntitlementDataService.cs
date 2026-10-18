using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for AccountEntitlement.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountEntitlementDataService(AppDbContext context) : BaseDataService<AccountEntitlementEntity>(context), IAccountEntitlementDataService
{
    private IQueryable<AccountEntitlementEntity> AccountEntitlements => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountEntitlementEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await AccountEntitlements.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
