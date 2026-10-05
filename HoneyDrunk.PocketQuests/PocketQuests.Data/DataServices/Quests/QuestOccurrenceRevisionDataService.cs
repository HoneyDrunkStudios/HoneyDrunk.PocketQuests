using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestOccurrenceRevision.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestOccurrenceRevisionDataService(AppDbContext context) : BaseDataService<QuestOccurrenceRevisionEntity>(context), IQuestOccurrenceRevisionDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestOccurrenceRevisionEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestOccurrenceRevisionEntity>> GetSelected(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
