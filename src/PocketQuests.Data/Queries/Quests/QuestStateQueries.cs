using Microsoft.EntityFrameworkCore;

namespace PocketQuests.Data.Queries.Quests;

internal static class QuestStateQueries
{
    internal static async Task<QuestStateRows> Read(AppDbContext context, Guid accountId, CancellationToken token)
    {
        var terms = context.QuestDefinitionRevision.Where(revision => revision.AccountId == accountId
            && (context.QuestDefinition.Any(head => head.AccountId == accountId && head.Id == revision.QuestDefinitionId && head.Revision == revision.Revision)
                || context.QuestOccurrence.Any(occurrence => occurrence.AccountId == accountId && occurrence.QuestDefinitionRevisionId == revision.Id)
                || context.QuestSeriesRevision.Any(series => series.AccountId == accountId && series.QuestDefinitionRevisionId == revision.Id
                    && context.QuestSeries.Any(head => head.AccountId == accountId && head.Id == series.QuestSeriesId && head.Revision == series.Revision))
                || context.QuestOccurrenceRevision.Any(occurrence => occurrence.AccountId == accountId && occurrence.QuestDefinitionRevisionId == revision.Id
                    && context.QuestCompletion.Any(completion => completion.AccountId == accountId && completion.QuestOccurrenceRevisionId == occurrence.Id))));
        var termIds = terms.Select(revision => revision.Id);
        return new()
        {
            Interests = await context.AccountInterest.Where(row => row.AccountId == accountId).OrderBy(row => row.Position).ToListAsync(token),
            Pauses = await context.AccountPause.Where(row => row.AccountId == accountId).OrderBy(row => row.CreationOrdinal).ToListAsync(token),
            Skills = await context.CustomSkill.Where(row => row.AccountId == accountId).OrderBy(row => row.CreationOrdinal).ToListAsync(token),
            Definitions = await context.QuestDefinition.Where(row => row.AccountId == accountId).OrderBy(row => row.CreationOrdinal).ToListAsync(token),
            DefinitionRevisions = await terms.OrderBy(row => row.Revision).ToListAsync(token),
            Attributes = await context.QuestDefinitionAttributeAllocation.Where(row => row.AccountId == accountId && termIds.Contains(row.QuestDefinitionRevisionId)).OrderBy(row => row.Position).ToListAsync(token),
            SkillAllocations = await context.QuestDefinitionSkillAllocation.Where(row => row.AccountId == accountId && termIds.Contains(row.QuestDefinitionRevisionId)).OrderBy(row => row.Position).ToListAsync(token),
            Series = await context.QuestSeries.Where(row => row.AccountId == accountId).OrderBy(row => row.CreationOrdinal).ToListAsync(token),
            SeriesRevisions = await context.QuestSeriesRevision.Where(row => row.AccountId == accountId).OrderBy(row => row.Revision).ToListAsync(token),
            Occurrences = await context.QuestOccurrence.Where(row => row.AccountId == accountId).OrderBy(row => row.CreationOrdinal).ToListAsync(token),
            OccurrenceRevisions = await context.QuestOccurrenceRevision.Where(row => row.AccountId == accountId && context.QuestCompletion.Any(completion => completion.AccountId == accountId && completion.QuestOccurrenceRevisionId == row.Id)).OrderBy(row => row.AccountMutationVersion).ToListAsync(token),
            Events = await context.QuestOccurrenceEvent.Where(row => row.AccountId == accountId && (row.EventCode == "Completed" || row.EventCode == "Undone" || row.EventCode == "DeadlineElapsed" || context.QuestCompletion.Any(completion => completion.AccountId == accountId && (completion.Id == row.Id || completion.UndoQuestOccurrenceEventId == row.Id)))).OrderBy(row => row.AccountMutationVersion).ToListAsync(token),
            Completions = await (from row in context.QuestCompletion
                                 join source in context.QuestOccurrenceEvent on new { row.AccountId, row.Id } equals new { source.AccountId, source.Id } into sources
                                 from source in sources.DefaultIfEmpty()
                                 where row.AccountId == accountId
                                 orderby source.AccountMutationVersion
                                 select row).ToListAsync(token),
            Undos = await (from row in context.QuestCompletion
                           join source in context.QuestOccurrenceEvent on new { row.AccountId, Id = row.UndoQuestOccurrenceEventId } equals new { source.AccountId, Id = (Guid?)source.Id } into sources
                           from source in sources.DefaultIfEmpty()
                           where row.AccountId == accountId && row.UndoQuestOccurrenceEventId != null
                           orderby source.AccountMutationVersion
                           select row).ToListAsync(token),
            CurrentDefinitions = await (from row in context.QuestDefinition
                                        join revision in context.QuestDefinitionRevision on new { row.AccountId, Id = row.Id, row.Revision } equals new { revision.AccountId, Id = revision.QuestDefinitionId, revision.Revision } into revisions
                                        from revision in revisions.DefaultIfEmpty()
                                        where row.AccountId == accountId && row.SystemQuestId == null
                                        orderby row.CreationOrdinal
                                        select new CurrentQuestDefinition(row, revision)).ToListAsync(token),
            CurrentSeries = await (from row in context.QuestSeries
                                   join revision in context.QuestSeriesRevision on new { row.AccountId, Id = row.Id, row.Revision } equals new { revision.AccountId, Id = revision.QuestSeriesId, revision.Revision } into revisions
                                   from revision in revisions.DefaultIfEmpty()
                                   where row.AccountId == accountId
                                   orderby row.CreationOrdinal
                                   select new CurrentQuestSeries(row, revision)).ToListAsync(token),
            CategoryProgress = await context.CategoryProgress.Where(row => row.AccountId == accountId).ToListAsync(token),
            Assessments = await (from row in context.SkillAssessment
                                 join receipt in context.CommandReceipt on new { row.AccountId, Id = row.CommandReceiptId } equals new { receipt.AccountId, receipt.Id }
                                 where row.AccountId == accountId
                                 orderby receipt.AppliedMutationVersion
                                 select row).ToListAsync(token),
            Zones = await (from row in context.TimeZoneChange
                           join receipt in context.CommandReceipt on new { row.AccountId, Id = row.CommandReceiptId } equals new { receipt.AccountId, receipt.Id }
                           where row.AccountId == accountId
                           orderby receipt.AppliedMutationVersion
                           select row).ToListAsync(token),
            PauseHistory = await context.QuestCommandHistory.Where(row => row.AccountId == accountId && row.CategoryId != null && (row.ActionCode == "pause" || row.ActionCode == "resume")).OrderBy(row => row.AccountMutationVersion).ToListAsync(token),
        };
    }
}
