using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Services.Quests.Mapping;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests;

internal static class OccurrenceChanges
{
    internal static void Apply(QuestMutation change, QuestStateRows rows, QuestTermHistory terms, QuestChanges changes)
    {
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var existing = rows.Occurrences.ToDictionary(row => row.Id);
        var configurations = QuestHistory.Series(change.Command.Action == PocketQuests.Domain.Commands.QuestActions.SaveDefinition
            ? rows.SeriesRevisions : rows.SeriesRevisions.Concat(changes.SeriesRevisions));
        foreach (var view in change.State.Occurrences)
        {
            var occurrence = view.Occurrence;
            var life = occurrence.Lifecycle ?? new();
            var term = terms.Ensure(occurrence.Quest);
            var prior = existing.GetValueOrDefault(occurrence.Id);
            var seriesRevision = life.SeriesId is { } seriesId && life.ScheduleVersion is { } scheduleVersion ? configurations[(seriesId, scheduleVersion)] : null;
            var row = OccurrenceMapping.ToEntity(change, view, term, prior, prior?.QuestSeriesRevisionId ?? seriesRevision?.Id, aggregate.Occurrences.IndexOf(occurrence) + 1);
            if (prior is not null && SameOccurrence(prior, row))
                continue;
            row.Revision = (prior?.Revision ?? 0) + 1;
            var (code, at) = QuestTransitions.Occurrence(occurrence, prior, recordedAt);
            if (prior is null)
                changes.Occurrences.Add(row);
            else
                row.ApplyTo(prior);
            var revisionId = QuestValues.Derived(account.Id, $"occurrence/{row.Id:D}/revision/{row.Revision}");
            changes.Revisions.Add(OccurrenceMapping.ToRevision(change, row, revisionId));
            changes.Events.Add(OccurrenceMapping.ToEvent(change, QuestValues.Derived(account.Id, $"transition/{command.OperationId:D}/{row.Id:D}"), row.Id, row.Revision, code, at));
        }
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
