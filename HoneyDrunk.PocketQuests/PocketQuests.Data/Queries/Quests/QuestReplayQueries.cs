using Microsoft.EntityFrameworkCore;

namespace PocketQuests.Data.Queries.Quests;

internal static class QuestReplayQueries
{
    internal static async Task<QuestReplayRows> Read(AppDbContext context, Guid accountId, long version, CancellationToken token)
    {
        var history = context.QuestCommandHistory.AsNoTracking().Where(row => row.AccountId == accountId && row.AccountMutationVersion <= version);
        var revisions = context.QuestOccurrenceRevision.AsNoTracking().Where(row => row.AccountId == accountId && history.Any(command => command.CompletionTermsRevisionId == row.Id));
        var terms = context.QuestDefinitionRevision.AsNoTracking().Where(row => row.AccountId == accountId
            && (history.Any(command => command.QuestDefinitionRevisionId == row.Id) || revisions.Any(revision => revision.QuestDefinitionRevisionId == row.Id)));
        var termIds = terms.Select(row => row.Id);
        return new()
        {
            InitialTimeZone = await context.QuestCommandHistory.Where(row => row.AccountId == accountId).OrderBy(row => row.AccountMutationVersion).Select(row => row.TimeZoneBefore).FirstOrDefaultAsync(token),
            History = await history.OrderBy(row => row.AccountMutationVersion).ToListAsync(token),
            OccurrenceRevisions = await revisions.ToListAsync(token),
            Definitions = await context.QuestDefinition.AsNoTracking().Where(row => row.AccountId == accountId && terms.Any(term => term.QuestDefinitionId == row.Id)).ToListAsync(token),
            DefinitionRevisions = await terms.OrderBy(row => row.Revision).ToListAsync(token),
            Attributes = await context.QuestDefinitionAttributeAllocation.AsNoTracking().Where(row => row.AccountId == accountId && termIds.Contains(row.QuestDefinitionRevisionId)).OrderBy(row => row.Position).ToListAsync(token),
            SkillAllocations = await context.QuestDefinitionSkillAllocation.AsNoTracking().Where(row => row.AccountId == accountId && termIds.Contains(row.QuestDefinitionRevisionId)).OrderBy(row => row.Position).ToListAsync(token),
            Skills = await context.CustomSkill.AsNoTracking().Where(row => row.AccountId == accountId).ToListAsync(token),
            CommandInterests = await context.QuestCommandInterest.AsNoTracking().Where(row => row.AccountId == accountId && history.Any(command => command.Id == row.QuestCommandHistoryId)).OrderBy(row => row.Position).ToListAsync(token),
        };
    }
}
