using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestSeries.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestSeriesDataService(AppDbContext context) : BaseDataService<QuestSeriesEntity>(context), IQuestSeriesDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestSeriesEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
