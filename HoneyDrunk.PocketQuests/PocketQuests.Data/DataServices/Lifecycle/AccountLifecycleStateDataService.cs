using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.DataServices.Lifecycle;

/// <summary>EF persistence and queries for AccountLifecycleState.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountLifecycleStateDataService(QuestDbContext context) : BaseDataService<AccountLifecycleStateEntity>(context), IAccountLifecycleStateDataService
{
    /// <inheritdoc />
    public Task<AccountLifecycleStateEntity?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken token = default) =>
        DbSet.SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountLifecycleStateEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
