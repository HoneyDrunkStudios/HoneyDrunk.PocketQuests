using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestDefinitionRevision.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestDefinitionRevisionDataService(AppDbContext context) : BaseDataService<QuestDefinitionRevisionEntity>(context), IQuestDefinitionRevisionDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionRevisionEntity>> GetSelectedAsync(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
