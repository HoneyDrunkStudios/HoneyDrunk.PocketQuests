using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestDefinition.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestDefinitionDataService(QuestDbContext context) : BaseDataService<QuestDefinitionEntity>(context), IQuestDefinitionDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionEntity>> GetSelectedAsync(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
