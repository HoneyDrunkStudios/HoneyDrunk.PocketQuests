using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestSeries.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestSeriesDataService(QuestDbContext context) : BaseDataService<QuestSeriesEntity>(context), IQuestSeriesDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestSeriesEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
