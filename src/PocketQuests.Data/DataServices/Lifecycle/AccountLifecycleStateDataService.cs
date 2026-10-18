using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.DataServices.Lifecycle;

/// <summary>EF persistence and queries for AccountLifecycleState.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountLifecycleStateDataService(AppDbContext context) : BaseDataService<AccountLifecycleStateEntity>(context), IAccountLifecycleStateDataService
{
    private IQueryable<AccountLifecycleStateEntity> AccountLifecycleStates => DbSet;

    /// <inheritdoc />
    public Task<AccountLifecycleStateEntity?> ReadCurrent(string identityUserId, CancellationToken token = default) =>
        AccountLifecycleStates.AsNoTracking().SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, token);

    /// <inheritdoc />
    public Task<AccountLifecycleStateEntity?> GetByIdentityUserId(string identityUserId, CancellationToken token = default) =>
        AccountLifecycleStates.SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountLifecycleStateEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await AccountLifecycleStates.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
