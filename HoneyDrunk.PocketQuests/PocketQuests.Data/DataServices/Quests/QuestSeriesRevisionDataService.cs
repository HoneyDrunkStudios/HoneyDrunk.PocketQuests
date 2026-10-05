using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestSeriesRevision.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestSeriesRevisionDataService(AppDbContext context) : BaseDataService<QuestSeriesRevisionEntity>(context), IQuestSeriesRevisionDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetCommittedByAccountId(Guid accountId, CancellationToken token = default) =>
        await DbSet.AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetSelected(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
