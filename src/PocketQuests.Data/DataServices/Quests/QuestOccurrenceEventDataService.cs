using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestOccurrenceEvent.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestOccurrenceEventDataService(AppDbContext context) : BaseDataService<QuestOccurrenceEventEntity>(context), IQuestOccurrenceEventDataService
{
    private IQueryable<QuestOccurrenceEventEntity> QuestOccurrenceEvents => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestOccurrenceEventEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await QuestOccurrenceEvents.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
