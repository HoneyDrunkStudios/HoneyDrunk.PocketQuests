using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for AccountPause.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountPauseDataService(AppDbContext context) : BaseDataService<AccountPauseEntity>(context), IAccountPauseDataService
{
    private IQueryable<AccountPauseEntity> AccountPauses => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountPauseEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await AccountPauses.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
