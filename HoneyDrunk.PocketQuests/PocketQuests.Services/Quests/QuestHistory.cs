using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Services.Quests;

internal static class QuestHistory
{
    internal static Dictionary<(Guid series, int version), QuestSeriesRevisionEntity> Series(IEnumerable<QuestSeriesRevisionEntity> rows) =>
        rows.GroupBy(row => (series: row.QuestSeriesId, version: row.ScheduleVersion)).ToDictionary(group => group.Key, group => group.MaxBy(row => row.Revision)!);

    internal static (Occurrence? occurrence, Quest? quest) CommandTargets(QuestMutation change)
    {
        var command = change.Command;
        var action = command.Action;
        var target = action == QuestActions.Accept ? change.Aggregate.Occurrences[^1]
            : command.OccurrenceId is { } id ? change.Aggregate.Occurrences.SingleOrDefault(row => row.Id == id) : null;
        Quest? quest = action switch
        {
            QuestActions.SaveDefinition => change.Aggregate.Definitions.Single(row => row.Quest.Id == command.Definition!.Id).Quest,
            QuestActions.ArchiveDefinition => change.Aggregate.Definitions.Single(row => row.Quest.Id == command.QuestId).Quest,
            QuestActions.SaveSeries => change.Aggregate.Schedule.Series.Single(row => row.Id == command.SeriesId).Quest,
            QuestActions.Accept or QuestActions.AcceptOffer or QuestActions.Abandon => target?.Quest,
            _ => null,
        };
        return (target, quest);
    }
}
