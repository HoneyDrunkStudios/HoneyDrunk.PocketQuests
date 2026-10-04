using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestCommandHistory.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestCommandHistoryDataService(QuestDbContext context) : BaseDataService<QuestCommandHistoryEntity>(context), IQuestCommandHistoryDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCommandHistoryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
