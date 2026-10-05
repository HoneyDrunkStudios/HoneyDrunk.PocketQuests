using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for AccountPause.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountPauseDataService(AppDbContext context) : BaseDataService<AccountPauseEntity>(context), IAccountPauseDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountPauseEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
