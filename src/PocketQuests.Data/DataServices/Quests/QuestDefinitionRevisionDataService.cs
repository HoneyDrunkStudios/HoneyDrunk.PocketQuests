using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestDefinitionRevision.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestDefinitionRevisionDataService(AppDbContext context) : BaseDataService<QuestDefinitionRevisionEntity>(context), IQuestDefinitionRevisionDataService
{
    private IQueryable<QuestDefinitionRevisionEntity> QuestDefinitionRevisions => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionRevisionEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await QuestDefinitionRevisions.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionRevisionEntity>> GetSelected(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await QuestDefinitionRevisions.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
