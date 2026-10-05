using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Quests;

internal static class CompletionChanges
{
    internal static void Apply(QuestMutation change, QuestStateRows rows, QuestChanges changes)
    {
        var existing = rows.Completions.Select(row => row.Id).ToHashSet();
        var occurrences = rows.Occurrences.Concat(changes.Occurrences).ToDictionary(row => row.Id);
        foreach (var completion in change.Aggregate.Completions.Where(item => !existing.Contains(item.Id)))
        {
            var occurrence = occurrences[completion.OccurrenceId];
            changes.Events.Add(OccurrenceMapping.ToEvent(change, completion.Id, completion.OccurrenceId, occurrence.Revision, "Completed", completion.RecordedAt));
            changes.Completions.Add(CompletionMapping.ToEntity(change, completion, occurrence));
        }

        var completedRows = rows.Completions.Concat(changes.Completions).ToDictionary(row => row.Id);
        foreach (var undo in change.Aggregate.Undos)
        {
            var row = completedRows[undo.CompletionId];
            if (row.UndoQuestOccurrenceEventId == undo.Id && row.UndoneAt == undo.RecordedAt)
                continue;
            if (row.UndoneAt is not null)
                throw new InvalidOperationException("A retained Undo cannot be replaced.");
            var occurrence = occurrences[row.QuestOccurrenceId];
            changes.Events.Add(OccurrenceMapping.ToEvent(change, undo.Id, row.QuestOccurrenceId, occurrence.Revision, "Undone", undo.RecordedAt));
            CompletionMapping.ApplyUndo(row, undo.Id, undo.RecordedAt, QuestClock.Max(row.ModifiedAt, change.Now));
        }
    }
}
