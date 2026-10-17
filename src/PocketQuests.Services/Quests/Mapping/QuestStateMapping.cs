using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Quests.Aggregates;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class QuestStateMapping
{
    internal static QuestAggregate ToModel(this QuestStateRows rows, AccountEntity account, IReadOnlyDictionary<Guid, Quest> terms, IReadOnlyList<string> pausedCategories)
    {
        var revisions = rows.SeriesRevisions.ToDictionary(row => row.Id);
        var occurrenceRevisions = rows.OccurrenceRevisions.ToDictionary(row => row.Id);
        return new(
            account.TimeZoneId,
            rows.Occurrences.Select(row => row.ToModel(terms, revisions)),
            rows.Completions.Select(row => new Completion(row.Id, row.QuestOccurrenceId, row.RecordedAt, terms[occurrenceRevisions[row.QuestOccurrenceRevisionId].QuestDefinitionRevisionId])),
            rows.Undos.Select(row => new UndoEvent(row.UndoQuestOccurrenceEventId!.Value, row.Id, row.UndoneAt!.Value)),
            rows.CurrentDefinitions.Select(row => new QuestDefinition(terms[row.Revision!.Id], row.Head.Revision, row.Head.ArchivedAt is not null)),
            rows.ToProfile(account),
            new ScheduleState(
                [.. rows.CurrentSeries.Select(current =>
                {
                    var row = current.Head;
                    var revision = current.Revision!;
                    return new QuestSeries(row.Id, terms[revision.QuestDefinitionRevisionId], QuestValues.DateText(revision.AnchorOn)!, Enum.Parse<Cadence>(revision.CadenceCode), revision.Interval, revision.ScheduleVersion, row.NextSequence, row.PauseDays, row.StoppedAt is not null, QuestValues.TimeText(revision.PlannedTime), revision.HasAutoAcceptPenalty, revision.EffectiveAt);
                })],
                [.. rows.Pauses.Select(row => new PauseWindow(row.CategoryId!, row.StartedAt, row.EndedAt))],
                [.. pausedCategories],
                account.IsAccountPaused));
    }
}
