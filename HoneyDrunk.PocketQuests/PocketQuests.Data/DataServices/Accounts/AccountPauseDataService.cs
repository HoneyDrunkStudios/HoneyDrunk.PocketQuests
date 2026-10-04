using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for AccountPause.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountPauseDataService(QuestDbContext context) : BaseDataService<AccountPauseEntity>(context), IAccountPauseDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountPauseEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
