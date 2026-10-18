using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestSeries.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestSeriesDataService(AppDbContext context) : BaseDataService<QuestSeriesEntity>(context), IQuestSeriesDataService
{
    private IQueryable<QuestSeriesEntity> QuestSeries => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestSeriesEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await QuestSeries.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
