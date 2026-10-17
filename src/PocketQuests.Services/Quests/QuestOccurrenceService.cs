using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Services.Quests.Mapping;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests;

internal sealed class QuestOccurrenceService(
    IQuestOccurrenceDataService occurrenceData, IQuestOccurrenceRevisionDataService revisionData,
    IQuestOccurrenceEventDataService eventData, IQuestCompletionDataService completionData)
{
    internal async Task<(IReadOnlyList<QuestOccurrenceEntity> occurrences, IReadOnlySet<Guid> eventIds)> Record(
        QuestMutation change, QuestStateRows rows, QuestTermHistory terms, IReadOnlyList<QuestSeriesRevisionEntity> seriesRevisions, CancellationToken token)
    {
        List<QuestOccurrenceEntity> newOccurrences = [];
        List<QuestOccurrenceRevisionEntity> newRevisions = [];
        List<QuestOccurrenceEventEntity> newEvents = [];
        List<QuestCompletionEntity> newCompletions = [];
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var existing = rows.Occurrences.ToDictionary(row => row.Id);
        var configurations = QuestHistory.Series(change.Command.Action == PocketQuests.Domain.Commands.QuestActions.SaveDefinition
            ? rows.SeriesRevisions : seriesRevisions);
        foreach (var view in change.State.Occurrences)
        {
            var occurrence = view.Occurrence;
            var life = occurrence.Lifecycle ?? new();
            var term = terms.Find(occurrence.Quest);
            var prior = existing.GetValueOrDefault(occurrence.Id);
            var seriesRevision = life.SeriesId is { } seriesId && life.ScheduleVersion is { } scheduleVersion ? configurations[(seriesId, scheduleVersion)] : null;
            var row = OccurrenceMapping.ToEntity(change, view, term, prior, prior?.QuestSeriesRevisionId ?? seriesRevision?.Id, aggregate.Occurrences.IndexOf(occurrence) + 1);
            if (prior is not null && SameOccurrence(prior, row))
                continue;
            row.Revision = (prior?.Revision ?? 0) + 1;
            var (code, at) = QuestTransitions.Occurrence(occurrence, prior, recordedAt);
            if (prior is null)
            {
                newOccurrences.Add(row);
            }
            else
            {
                row.ApplyTo(prior);
                prior.ModifiedAt = QuestClock.Max(prior.ModifiedAt, change.Now);
            }

            var revisionId = QuestValues.Derived(account.Id, $"occurrence/{row.Id:D}/revision/{row.Revision}");
            newRevisions.Add(OccurrenceMapping.ToRevision(change, row, revisionId));
            newEvents.Add(OccurrenceMapping.ToEvent(change, QuestValues.Derived(account.Id, $"transition/{command.OperationId:D}/{row.Id:D}"), row.Id, row.Revision, code, at));
        }

        var completedIds = rows.Completions.Select(row => row.Id).ToHashSet();
        var occurrences = rows.Occurrences.Concat(newOccurrences).ToDictionary(row => row.Id);
        foreach (var completion in change.Aggregate.Completions.Where(item => !completedIds.Contains(item.Id)))
        {
            var occurrence = occurrences[completion.OccurrenceId];
            newEvents.Add(OccurrenceMapping.ToEvent(change, completion.Id, completion.OccurrenceId, occurrence.Revision, "Completed", completion.RecordedAt));
            newCompletions.Add(CompletionMapping.ToEntity(change, completion, occurrence));
        }

        var completedRows = rows.Completions.Concat(newCompletions).ToDictionary(row => row.Id);
        foreach (var undo in change.Aggregate.Undos)
        {
            var row = completedRows[undo.CompletionId];
            if (row.UndoQuestOccurrenceEventId == undo.Id && row.UndoneAt == undo.RecordedAt)
                continue;
            if (row.UndoneAt is not null)
                throw new InvalidOperationException("A retained Undo cannot be replaced.");
            var occurrence = occurrences[row.QuestOccurrenceId];
            newEvents.Add(OccurrenceMapping.ToEvent(change, undo.Id, row.QuestOccurrenceId, occurrence.Revision, "Undone", undo.RecordedAt));
            CompletionMapping.ApplyUndo(row, undo.Id, undo.RecordedAt);
            row.ModifiedAt = QuestClock.Max(row.ModifiedAt, change.Now);
        }

        foreach (var row in newOccurrences)
            row.CreatedAt = row.ModifiedAt = change.Now;
        foreach (var row in newRevisions)
            row.CreatedAt = change.Now;
        foreach (var row in newEvents)
            row.CreatedAt = change.Now;
        foreach (var row in newCompletions)
            row.CreatedAt = row.ModifiedAt = change.Now;

        await occurrenceData.AddRangeAsync(newOccurrences, token);
        await revisionData.AddRangeAsync(newRevisions, token);
        await eventData.AddRangeAsync(newEvents, token);
        await completionData.AddRangeAsync(newCompletions, token);
        return (occurrences.Values.ToArray(), rows.Events.Select(row => row.Id).Concat(newEvents.Select(row => row.Id)).ToHashSet());
    }

    private static bool SameOccurrence(QuestOccurrenceEntity first, QuestOccurrenceEntity second) =>
        first.Id == second.Id
        && first.AccountId == second.AccountId
        && first.QuestDefinitionId == second.QuestDefinitionId
        && first.QuestDefinitionRevisionId == second.QuestDefinitionRevisionId
        && first.CategoryId == second.CategoryId
        && first.DueOn == second.DueOn
        && first.PlannedTime == second.PlannedTime
        && first.DeadlineAt == second.DeadlineAt
        && first.DeadlineTimeZoneId == second.DeadlineTimeZoneId
        && first.StateCode == second.StateCode
        && first.AcceptedAt == second.AcceptedAt
        && first.FrozenAt == second.FrozenAt
        && first.IsIndividuallyFrozen == second.IsIndividuallyFrozen
        && first.AbandonedAt == second.AbandonedAt
        && first.LockedLoss == second.LockedLoss
        && first.LossCategoryId == second.LossCategoryId
        && first.Revision == second.Revision
        && first.QuestSeriesId == second.QuestSeriesId
        && first.QuestSeriesRevisionId == second.QuestSeriesRevisionId
        && first.SeriesSequence == second.SeriesSequence
        && first.ParentQuestOccurrenceId == second.ParentQuestOccurrenceId
        && first.SourceSyncAnchorId == second.SourceSyncAnchorId
        && first.OriginatedAt == second.OriginatedAt
        && first.CreationOrdinal == second.CreationOrdinal
        && first.OriginatedOffsetMinutes == second.OriginatedOffsetMinutes;
}
