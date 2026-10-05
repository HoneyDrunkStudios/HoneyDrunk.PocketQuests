using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Synchronization;

/// <summary>EF persistence and queries for SyncAnchor.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SyncAnchorDataService(AppDbContext context) : BaseDataService<SyncAnchorEntity>(context), ISyncAnchorDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncAnchorEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
