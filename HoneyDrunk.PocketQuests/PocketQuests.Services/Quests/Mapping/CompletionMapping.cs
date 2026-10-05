using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class CompletionMapping
{
    internal static Completion ToModel(this QuestCompletionEntity row, Quest terms) =>
        new(row.Id, row.QuestOccurrenceId, row.RecordedAt, terms);

    internal static QuestCompletionEntity ToEntity(QuestMutation change, Completion completion, QuestOccurrenceEntity occurrence)
    {
        return new QuestCompletionEntity
        {
            Id = completion.Id,
            AccountId = change.Account.Id,
            QuestOccurrenceId = completion.OccurrenceId,
            QuestOccurrenceRevisionId = QuestValues.Derived(change.Account.Id, $"occurrence/{occurrence.Id:D}/revision/{occurrence.Revision}"),
            RecordedAt = completion.RecordedAt,
        };
    }

    internal static void ApplyUndo(QuestCompletionEntity row, Guid eventId, DateTimeOffset recordedAt)
    {
        row.UndoneAt = recordedAt;
        row.UndoQuestOccurrenceEventId = eventId;
    }
}
