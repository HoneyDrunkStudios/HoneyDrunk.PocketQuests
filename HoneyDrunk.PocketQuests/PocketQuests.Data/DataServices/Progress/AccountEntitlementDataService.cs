using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for AccountEntitlement.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountEntitlementDataService(QuestDbContext context) : BaseDataService<AccountEntitlementEntity>(context), IAccountEntitlementDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountEntitlementEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
