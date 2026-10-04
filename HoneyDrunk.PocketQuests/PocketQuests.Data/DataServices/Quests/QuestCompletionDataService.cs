using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestCompletion.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestCompletionDataService(QuestDbContext context) : BaseDataService<QuestCompletionEntity>(context), IQuestCompletionDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCompletionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCompletionEntity>> GetCurrentForOccurrencesAsync(Guid accountId, Guid[] ids, DateTimeOffset at, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.QuestOccurrenceId) && row.UndoneAt == null && row.RecordedAt <= at).ToListAsync(token);
    }
}
