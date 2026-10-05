using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Queries.Quests;

/// <summary>Historical sources for one requested version; no current reward projections.</summary>
public sealed class QuestReplayRows : QuestTermsRows
{
    /// <summary>Gets the owned QuestOccurrenceRevision rows.</summary>
    public required IReadOnlyList<QuestOccurrenceRevisionEntity> OccurrenceRevisions { get; init; }

    /// <summary>Gets the owned QuestCommandHistory rows.</summary>
    public required IReadOnlyList<QuestCommandHistoryEntity> History { get; init; }

    /// <summary>Gets the owned QuestCommandInterest rows.</summary>
    public required IReadOnlyList<QuestCommandInterestEntity> CommandInterests { get; init; }

    /// <summary>Gets the first retained command timezone, including for version zero.</summary>
    public string? InitialTimeZone { get; init; }
}
