using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Services.Quests.Mapping;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests;

internal static class QuestHistory
{
    internal static Dictionary<(Guid series, int version), QuestSeriesRevisionEntity> Series(IEnumerable<QuestSeriesRevisionEntity> rows) =>
        rows.GroupBy(row => (series: row.QuestSeriesId, version: row.ScheduleVersion)).ToDictionary(group => group.Key, group => group.MaxBy(row => row.Revision)!);

    internal static void RecordCommand(QuestMutation change, QuestStateRows rows, QuestTermHistory terms, QuestChanges changes)
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
        var termId = quest is null ? (Guid?)null : terms.Ensure(quest).Id;
        var occurrence = action == QuestActions.Complete && command.RecordedTime is not null
            ? rows.Occurrences.Concat(changes.Occurrences).Single(row => row.Id == target!.Id) : null;
        var completionRevision = occurrence is null ? (Guid?)null : QuestValues.Derived(change.Account.Id, $"occurrence/{occurrence.Id:D}/revision/{occurrence.Revision}");
        var skillName = action == QuestActions.SaveSkill ? change.Aggregate.Profile.CustomSkills!.Single(row => row.Id == command.SkillId).Name : null;
        change.ApplyToHistory(changes.History, target, quest, termId, completionRevision, skillName);
    }
}
