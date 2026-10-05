using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Schedules;
using PocketQuests.Services.Quests.Mapping;
using System.Data;

namespace PocketQuests.Services.Quests;

/// <summary>Bounded occurrence reads for private consumers; no public route is introduced.</summary>
/// <param name="data">Account transaction and selected terms.</param>
/// <param name="occurrences">Keyset occurrence query.</param>
/// <param name="completions">Selected active completion sources.</param>
/// <param name="revisions">Selected occurrence revisions.</param>
/// <param name="series">Selected series provenance.</param>
/// <param name="quests">Shared verified account access.</param>
public sealed class OccurrenceReadService(IAccountDataService data, IQuestOccurrenceDataService occurrences, IQuestCompletionDataService completions, IQuestOccurrenceRevisionDataService revisions, IQuestSeriesRevisionDataService series, QuestService quests)
{
    internal Task<QuestOccurrencePage> Read(AccountIdentity identity, int after, int size, DateTimeOffset now, CancellationToken token = default)
    {
        if (after < 0 || size is < 1 or > 100)
            throw new QuestValidationException("Use a nonnegative cursor and a page size between one and one hundred.");
        return data.ExecuteInTransaction(Page, token, IsolationLevel.RepeatableRead);

        async Task<QuestOccurrencePage> Page(CancellationToken cancellationToken)
        {
            await quests.RequireAccess(identity, false, cancellationToken);
            var account = await quests.Account(identity, cancellationToken);
            return await ReadPage(account, after, size, QuestClock.Max(now, account.LastRecordedAt), cancellationToken);
        }
    }

    private async Task<QuestOccurrencePage> ReadPage(AccountEntity account, int after, int size, DateTimeOffset at, CancellationToken token)
    {
        var selected = await occurrences.GetPageAsync(account.Id, after, size, token);
        var next = selected.Count > size ? selected[size - 1].CreationOrdinal : (int?)null;
        var rows = selected.Take(size).ToArray();
        var ids = rows.Select(row => row.Id).ToArray();
        var completionRows = await completions.GetCurrentForOccurrencesAsync(account.Id, ids, at, token);
        var completionRevisionIds = completionRows.Select(row => row.QuestOccurrenceRevisionId).ToArray();
        var completionRevisions = (await revisions.GetSelectedAsync(account.Id, completionRevisionIds, token)).ToDictionary(row => row.Id);
        var termIds = rows.Select(row => row.QuestDefinitionRevisionId).Concat(completionRevisions.Values.Select(row => row.QuestDefinitionRevisionId)).Distinct().ToArray();
        var terms = QuestTerms.Read(await data.ReadSelectedTerms(account.Id, termIds, token));
        var seriesIds = rows.Where(row => row.QuestSeriesRevisionId is not null).Select(row => row.QuestSeriesRevisionId!.Value).Distinct().ToArray();
        var seriesRows = (await series.GetSelectedAsync(account.Id, seriesIds, token)).ToDictionary(row => row.Id);
        var views = rows.Select(row =>
        {
            var occurrence = row.ToModel(terms, seriesRows);
            var source = completionRows.SingleOrDefault(completion => completion.QuestOccurrenceId == row.Id);
            var completion = source is null ? null : new Completion(source.Id, row.Id, source.RecordedAt, terms[completionRevisions[source.QuestOccurrenceRevisionId].QuestDefinitionRevisionId]);
            var status = row.AcceptedAt is null ? QuestStatus.Offered : completion is not null ? QuestStatus.Completed
                : row.AbandonedAt is not null ? QuestStatus.Abandoned : row.FrozenAt is not null ? QuestStatus.Frozen
                : row.DeadlineAt <= at ? QuestStatus.Missed : QuestStatus.Active;
            return new OccurrenceView(occurrence, status, completion, completion is not null && at >= completion.RecordedAt && at < completion.RecordedAt.AddHours(24), occurrence.DueDate is not null && occurrence.PlannedTime is not null ? Scheduling.Planned(occurrence.DueDate, occurrence.PlannedTime, row.DeadlineTimeZoneId!) : null);
        });
        return new([.. views], next, account.MutationVersion, account.ProjectionAsOfAt, account.HasPendingReconciliation);
    }
}
