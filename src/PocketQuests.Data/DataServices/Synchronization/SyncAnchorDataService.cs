using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Synchronization;

/// <summary>EF persistence and queries for SyncAnchor.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SyncAnchorDataService(AppDbContext context) : BaseDataService<SyncAnchorEntity>(context), ISyncAnchorDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncAnchorEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
