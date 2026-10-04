using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Queries.Accounts;

/// <summary>Owned relational rows used to construct current state or replay history; contains no business models.</summary>
public sealed record AccountStateData(
    IReadOnlyList<QuestDefinitionEntity> QuestDefinitionRows,
    IReadOnlyList<QuestDefinitionRevisionEntity> QuestDefinitionRevisionRows,
    IReadOnlyList<CustomSkillEntity> CustomSkillRows,
    IReadOnlyList<CommandReceiptEntity> CommandReceiptRows,
    IReadOnlyList<SkillAssessmentEntity> SkillAssessmentRows,
    IReadOnlyList<TimeZoneChangeEntity> TimeZoneChangeRows,
    IReadOnlyList<AccountInterestEntity> AccountInterestRows,
    IReadOnlyList<QuestSeriesEntity> QuestSeriesRows,
    IReadOnlyList<QuestSeriesRevisionEntity> QuestSeriesRevisionRows,
    IReadOnlyList<AccountPauseEntity> AccountPauseRows,
    IReadOnlyList<CategoryProgressEntity> CategoryProgressRows,
    IReadOnlyList<QuestCommandHistoryEntity> QuestCommandHistoryRows,
    IReadOnlyList<QuestOccurrenceEntity> QuestOccurrenceRows,
    IReadOnlyList<QuestOccurrenceRevisionEntity> QuestOccurrenceRevisionRows,
    IReadOnlyList<QuestOccurrenceEventEntity> QuestOccurrenceEventRows,
    IReadOnlyList<QuestCompletionEntity> QuestCompletionRows,
    IReadOnlyList<QuestCommandInterestEntity> QuestCommandInterestRows);
