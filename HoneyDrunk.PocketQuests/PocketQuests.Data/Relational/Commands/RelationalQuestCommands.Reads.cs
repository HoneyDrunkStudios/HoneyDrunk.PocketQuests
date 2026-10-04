using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Bounded current reads use the account/creation index and load only each page's referenced terms.</summary>
public sealed partial class RelationalQuestCommands
{
    /// <summary>Reads committed occurrences without creating accounts, reconciling deliveries, saving projections or taking the command lock.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="after">Exclusive creation-order cursor, initially zero.</param>
    /// <param name="size">Page size from one through one hundred.</param>
    /// <param name="now">Current clock for status and Undo availability.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The requested bounded page and its materialization watermark.</returns>
    public async Task<QuestOccurrencePage> ReadOccurrences(AccountIdentity identity, int after, int size, DateTimeOffset now, CancellationToken token = default)
    {
        if (after < 0 || size is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(size), "Use a nonnegative cursor and a page size between one and one hundred.");
        await using var session = await Open(identity, token, readOnly: true);
        var account = await Account(session, identity, token);
        var projectionAt = Max(now, account.LastRecordedAt);
        var db = session.Context;
        var rows = await db.Set<QuestOccurrenceEntity>().Where(r => r.AccountId == account.Id && r.CreationOrdinal > after)
            .OrderBy(r => r.CreationOrdinal).Take(size + 1).ToListAsync(token);
        var next = rows.Count > size ? rows[size - 1].CreationOrdinal : (int?)null;
        rows = [.. rows.Take(size)];
        var ids = rows.Select(r => r.Id).ToArray();
        var completions = await db.Set<QuestCompletionEntity>().Where(r => r.AccountId == account.Id && ids.Contains(r.QuestOccurrenceId) && r.UndoneAt == null && r.RecordedAt <= projectionAt).ToListAsync(token);
        var completionRevisionIds = completions.Select(r => r.QuestOccurrenceRevisionId).ToArray();
        var completionRevisions = await db.Set<QuestOccurrenceRevisionEntity>().Where(r => r.AccountId == account.Id && completionRevisionIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, token);
        var termIds = rows.Select(r => r.QuestDefinitionRevisionId).Concat(completionRevisions.Values.Select(r => r.QuestDefinitionRevisionId)).Distinct().ToArray();
        var terms = await ReadTerms(session, account.Id, token, termIds);
        var seriesIds = rows.Where(r => r.QuestSeriesRevisionId is not null).Select(r => r.QuestSeriesRevisionId!.Value).Distinct().ToArray();
        var series = await db.Set<QuestSeriesRevisionEntity>().Where(r => r.AccountId == account.Id && seriesIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, token);
        var views = rows.Select(row =>
        {
            var occurrence = CurrentOccurrence(row, terms, series);
            var completionRow = completions.SingleOrDefault(c => c.QuestOccurrenceId == row.Id);
            var completion = completionRow is null ? null : new Completion(completionRow.Id, row.Id, completionRow.RecordedAt, terms[completionRevisions[completionRow.QuestOccurrenceRevisionId].QuestDefinitionRevisionId]);
            var status = row.AcceptedAt is null ? QuestStatus.Offered : completion is not null ? QuestStatus.Completed
                : row.AbandonedAt is not null ? QuestStatus.Abandoned : row.FrozenAt is not null ? QuestStatus.Frozen
                : row.DeadlineAt <= projectionAt ? QuestStatus.Missed : QuestStatus.Active;
            return new OccurrenceView(occurrence, status, completion, completion is not null && projectionAt >= completion.RecordedAt && projectionAt < completion.RecordedAt.AddHours(24), occurrence.DueDate is not null && occurrence.PlannedTime is not null ? Scheduling.Planned(occurrence.DueDate, occurrence.PlannedTime, row.DeadlineTimeZoneId!) : null);
        }).ToArray();
        await session.Transaction.CommitAsync(token);
        return new([.. views], next, account.MutationVersion, account.ProjectionAsOfAt, account.HasPendingReconciliation);
    }
}
