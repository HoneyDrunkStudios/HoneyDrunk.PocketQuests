using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestOccurrenceEvent.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestOccurrenceEventDataService(AppDbContext context) : BaseDataService<QuestOccurrenceEventEntity>(context), IQuestOccurrenceEventDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestOccurrenceEventEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
