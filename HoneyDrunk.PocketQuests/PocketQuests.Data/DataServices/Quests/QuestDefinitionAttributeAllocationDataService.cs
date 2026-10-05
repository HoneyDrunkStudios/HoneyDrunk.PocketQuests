using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestDefinitionAttributeAllocation.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestDefinitionAttributeAllocationDataService(AppDbContext context) : BaseDataService<QuestDefinitionAttributeAllocationEntity>(context), IQuestDefinitionAttributeAllocationDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionAttributeAllocationEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinitionAttributeAllocationEntity>> GetSelected(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.QuestDefinitionRevisionId)).ToListAsync(token);
    }
}
