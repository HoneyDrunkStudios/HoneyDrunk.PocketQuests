using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestCommandInterest.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestCommandInterestDataService(AppDbContext context) : BaseDataService<QuestCommandInterestEntity>(context), IQuestCommandInterestDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCommandInterestEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
