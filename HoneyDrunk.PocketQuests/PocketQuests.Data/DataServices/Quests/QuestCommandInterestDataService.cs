using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestCommandInterest.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestCommandInterestDataService(QuestDbContext context) : BaseDataService<QuestCommandInterestEntity>(context), IQuestCommandInterestDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCommandInterestEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
