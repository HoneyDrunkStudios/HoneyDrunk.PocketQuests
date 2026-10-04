using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestOccurrenceEvent.</summary>
public interface IQuestOccurrenceEventDataService : IBaseDataService<QuestOccurrenceEventEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestOccurrenceEventEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
