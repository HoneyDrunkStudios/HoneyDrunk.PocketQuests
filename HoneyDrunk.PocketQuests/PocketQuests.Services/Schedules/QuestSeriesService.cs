using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Schedules.Mapping;

namespace PocketQuests.Services.Schedules;

internal sealed class QuestSeriesService(IQuestSeriesDataService seriesData, IQuestSeriesRevisionDataService revisionData)
{
    internal async Task<IReadOnlyList<QuestSeriesRevisionEntity>> Record(QuestMutation change, QuestStateRows rows, QuestTermHistory terms, CancellationToken token)
    {
        List<QuestSeriesEntity> newSeries = [];
        List<QuestSeriesRevisionEntity> newRevisions = [];
        var heads = rows.Series.ToDictionary(row => row.Id);
        var revisions = rows.CurrentSeries.ToDictionary(row => row.Head.Id, row => row.Revision!);
        foreach (var series in change.Aggregate.Schedule.Series)
        {
            var term = terms.Find(series.Quest);
            var prior = heads.GetValueOrDefault(series.Id);
            var original = revisions.GetValueOrDefault(series.Id);
            var receiptId = change.ReceiptId ?? original?.CommandReceiptId
                ?? throw new InvalidOperationException("An internal transition cannot introduce new series configuration.");
            var revision = series.ToRevision(change, term.Id, prior?.Revision ?? 1, receiptId);
            if (original is null || !SameConfiguration(original, revision))
            {
                revision = series.ToRevision(change, term.Id, checked((prior?.Revision ?? 0) + 1), receiptId);
                newRevisions.Add(revision);
            }
            else
            {
                revision = original;
            }

            var next = QuestCalendar.NextDelivery(series);
            var ordinal = change.Aggregate.Schedule.Series.IndexOf(series) + 1;
            if (prior is null)
            {
                var head = series.ToHead(change, term.QuestDefinitionId, revision.Revision, ordinal, next);
                head.StoppedAt = series.Stopped ? change.RecordedAt : null;
                newSeries.Add(head);
            }
            else if (prior.QuestDefinitionId != term.QuestDefinitionId || prior.Revision != revision.Revision
                || prior.NextSequence != series.NextSequence || prior.PauseDays != series.PauseDays
                || prior.NextDeliveryOn != next || (prior.StoppedAt is not null) != series.Stopped)
            {
                series.ApplyTo(prior, term.QuestDefinitionId, revision.Revision, next);
                prior.StoppedAt = series.Stopped ? prior.StoppedAt ?? change.RecordedAt : null;
                prior.ModifiedAt = QuestClock.Max(prior.ModifiedAt, change.Now);
            }
        }

        foreach (var row in newSeries)
            row.CreatedAt = row.ModifiedAt = change.Now;
        foreach (var row in newRevisions)
            row.CreatedAt = change.Now;

        await seriesData.AddRangeAsync(newSeries, token);
        await revisionData.AddRangeAsync(newRevisions, token);
        return [.. rows.SeriesRevisions, .. newRevisions];
    }

    private static bool SameConfiguration(QuestSeriesRevisionEntity first, QuestSeriesRevisionEntity second) =>
        first.QuestDefinitionRevisionId == second.QuestDefinitionRevisionId
        && first.CadenceCode == second.CadenceCode
        && first.Interval == second.Interval
        && first.AnchorOn == second.AnchorOn
        && first.PlannedTime == second.PlannedTime
        && first.HasAutoAcceptPenalty == second.HasAutoAcceptPenalty
        && first.EffectiveAt == second.EffectiveAt
        && first.ScheduleVersion == second.ScheduleVersion;
}
