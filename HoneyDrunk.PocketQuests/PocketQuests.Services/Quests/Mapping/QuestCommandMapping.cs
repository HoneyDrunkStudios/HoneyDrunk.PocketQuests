using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Domain.Models.Synchronization;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class QuestCommandMapping
{
    internal static QuestCommand ToCommand(this QuestCommandHistoryEntity row, IReadOnlyDictionary<Guid, Quest> terms, IReadOnlyDictionary<Guid, string> customKeys, ILookup<Guid, string> interests)
    {
        Quest? quest = row.QuestDefinitionRevisionId is { } termId ? terms[termId] : null;
        return new(
                row.Id,
                row.ActionCode,
                row.QuestOccurrenceId,
                row.ActionCode is QuestActions.Accept or QuestActions.SaveSeries or QuestActions.ArchiveDefinition ? quest?.Id : null,
                QuestValues.DateText(row.DueOn),
                row.QuestCompletionId,
                row.ActionCode == QuestActions.SaveDefinition ? quest : null,
                row.ExpectedRevision,
                row.SystemSkillId ?? (row.CustomSkillId is { } custom ? customKeys[custom] : null),
                row.ExperienceCode is null ? null : Enum.Parse<Experience>(row.ExperienceCode),
                row.ActionCode == QuestActions.Interests ? [.. interests[row.Id]] : null,
                QuestValues.TimeText(row.PlannedTime),
                row.ParentQuestOccurrenceId,
                row.ProfileRewardId,
                row.QuestSeriesId,
                row.CadenceCode is null ? null : Enum.Parse<Cadence>(row.CadenceCode),
                row.Interval,
                row.CategoryId,
                row.ConfirmPenalty,
                row.AcceptedLoss,
                row.SourceSyncAnchorId is { } anchor ? new RecordedActionTime(anchor, Guid.Empty, 0, 0, row.RecordedAt) : null,
                row.SkillName,
                row.HasAcceptedTerms ? quest : null,
                row.NewTimeZone,
                row.ExpectedTimeZone,
                row.ConfirmZoneChange,
                row.HasExpiryWarnings);
    }
}
