using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestOccurrence.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestOccurrenceDataService(AppDbContext context) : BaseDataService<QuestOccurrenceEntity>(context), IQuestOccurrenceDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestOccurrenceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestOccurrenceEntity>> GetPageAsync(Guid accountId, int after, int size, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && row.CreationOrdinal > after)
            .OrderBy(row => row.CreationOrdinal).Take(size + 1).ToListAsync(token);
    }
}
