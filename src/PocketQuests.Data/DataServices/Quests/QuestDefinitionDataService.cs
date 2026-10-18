using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestDefinition.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestDefinitionDataService(AppDbContext context) : BaseDataService<QuestDefinitionEntity>(context), IQuestDefinitionDataService
{
    private IQueryable<QuestDefinitionEntity> QuestDefinitions => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await QuestDefinitions.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionEntity>> GetSelected(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await QuestDefinitions.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
