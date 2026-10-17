using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Queries.Quests;

internal static class QuestTermsQueries
{
    internal static Task<QuestTermsRows> Read(AppDbContext context, Guid accountId, Guid[] definitionIds, CancellationToken token) =>
        Read(context, accountId, context.QuestDefinitionRevision.Where(row => row.AccountId == accountId && definitionIds.Contains(row.QuestDefinitionId)), token);

    internal static Task<QuestTermsRows> ReadSelected(AppDbContext context, Guid accountId, Guid[] revisionIds, CancellationToken token) =>
        Read(context, accountId, context.QuestDefinitionRevision.AsNoTracking().Where(row => row.AccountId == accountId && revisionIds.Contains(row.Id)), token);

    private static async Task<QuestTermsRows> Read(AppDbContext context, Guid accountId, IQueryable<QuestDefinitionRevisionEntity> revisions, CancellationToken token)
    {
        var definitionIds = revisions.Select(row => row.QuestDefinitionId);
        var ids = revisions.Select(row => row.Id);
        var skills = context.QuestDefinitionSkillAllocation.Where(row => row.AccountId == accountId && ids.Contains(row.QuestDefinitionRevisionId));
        return new()
        {
            Definitions = await context.QuestDefinition.Where(row => row.AccountId == accountId && definitionIds.Contains(row.Id)).ToListAsync(token),
            DefinitionRevisions = await revisions.OrderBy(row => row.Revision).ToListAsync(token),
            Attributes = await context.QuestDefinitionAttributeAllocation.Where(row => row.AccountId == accountId && ids.Contains(row.QuestDefinitionRevisionId)).OrderBy(row => row.Position).ToListAsync(token),
            SkillAllocations = await skills.OrderBy(row => row.Position).ToListAsync(token),
            Skills = await context.CustomSkill.Where(row => row.AccountId == accountId && skills.Any(allocation => allocation.CustomSkillId == row.Id)).ToListAsync(token),
        };
    }
}
