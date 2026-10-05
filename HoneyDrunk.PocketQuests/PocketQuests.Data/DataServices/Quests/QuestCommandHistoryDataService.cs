using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestCommandHistory.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestCommandHistoryDataService(AppDbContext context) : BaseDataService<QuestCommandHistoryEntity>(context), IQuestCommandHistoryDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCommandHistoryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
