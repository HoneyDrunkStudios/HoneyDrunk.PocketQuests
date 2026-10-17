using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Queries.Quests;

/// <summary>Current source rows needed to calculate account state; no receipts or complete command history.</summary>
public sealed class QuestStateRows : QuestTermsRows
{
    /// <summary>Gets the owned AccountInterest rows.</summary>
    public required IReadOnlyList<AccountInterestEntity> Interests { get; init; }

    /// <summary>Gets the owned AccountPause rows.</summary>
    public required IReadOnlyList<AccountPauseEntity> Pauses { get; init; }

    /// <summary>Gets custom definition heads with their current revision, retaining missing links for validation.</summary>
    public required IReadOnlyList<CurrentQuestDefinition> CurrentDefinitions { get; init; }

    /// <summary>Gets series heads with their current revision, retaining missing links for validation.</summary>
    public required IReadOnlyList<CurrentQuestSeries> CurrentSeries { get; init; }

    /// <summary>Gets undone completions ordered by their Undo event mutation.</summary>
    public required IReadOnlyList<QuestCompletionEntity> Undos { get; init; }

    /// <summary>Gets the owned QuestSeries rows.</summary>
    public required IReadOnlyList<QuestSeriesEntity> Series { get; init; }

    /// <summary>Gets the owned QuestSeriesRevision rows.</summary>
    public required IReadOnlyList<QuestSeriesRevisionEntity> SeriesRevisions { get; init; }

    /// <summary>Gets the owned QuestOccurrence rows.</summary>
    public required IReadOnlyList<QuestOccurrenceEntity> Occurrences { get; init; }

    /// <summary>Gets the owned QuestOccurrenceRevision rows.</summary>
    public required IReadOnlyList<QuestOccurrenceRevisionEntity> OccurrenceRevisions { get; init; }

    /// <summary>Gets the owned QuestOccurrenceEvent rows.</summary>
    public required IReadOnlyList<QuestOccurrenceEventEntity> Events { get; init; }

    /// <summary>Gets the owned QuestCompletion rows.</summary>
    public required IReadOnlyList<QuestCompletionEntity> Completions { get; init; }

    /// <summary>Gets the owned CategoryProgress rows.</summary>
    public required IReadOnlyList<CategoryProgressEntity> CategoryProgress { get; init; }

    /// <summary>Gets skill assessments in successful mutation order.</summary>
    public required IReadOnlyList<SkillAssessmentEntity> Assessments { get; init; }

    /// <summary>Gets time zone changes in successful mutation order.</summary>
    public required IReadOnlyList<TimeZoneChangeEntity> Zones { get; init; }

    /// <summary>Gets only ordered category pause/resume inputs.</summary>
    public required IReadOnlyList<QuestCommandHistoryEntity> PauseHistory { get; init; }
}
