using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class CompletionMapping
{
    internal static QuestCompletionEntity ToEntity(QuestMutation change, Completion completion, QuestOccurrenceEntity occurrence)
    {
        return new QuestCompletionEntity
        {
            Id = completion.Id,
            AccountId = change.Account.Id,
            QuestOccurrenceId = completion.OccurrenceId,
            QuestOccurrenceRevisionId = QuestValues.Derived(change.Account.Id, $"occurrence/{occurrence.Id:D}/revision/{occurrence.Revision}"),
            RecordedAt = completion.RecordedAt,
            CreatedAt = change.Now,
            ModifiedAt = change.Now,
        };
    }

    internal static void ApplyUndo(QuestCompletionEntity row, Guid eventId, DateTimeOffset recordedAt, DateTimeOffset modifiedAt)
    {
        row.UndoneAt = recordedAt;
        row.UndoQuestOccurrenceEventId = eventId;
        row.ModifiedAt = modifiedAt;
    }
}
