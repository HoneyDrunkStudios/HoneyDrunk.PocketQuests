using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Queries.Accounts;

internal static class AccountQueries
{
    internal static async Task<AccountStateData> ReadStateAsync(QuestDbContext context, Guid accountId, CancellationToken token) => new(
        await context.Set<QuestDefinitionEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestDefinitionRevisionEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<CustomSkillEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<CommandReceiptEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<SkillAssessmentEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<TimeZoneChangeEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<AccountInterestEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestSeriesEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestSeriesRevisionEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<AccountPauseEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<CategoryProgressEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestCommandHistoryEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestOccurrenceEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestOccurrenceRevisionEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestOccurrenceEventEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestCompletionEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
        await context.Set<QuestCommandInterestEntity>().AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token));
}
