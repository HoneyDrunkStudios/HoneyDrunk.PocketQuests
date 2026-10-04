using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestCommandHistory.</summary>
public interface IQuestCommandHistoryDataService : IBaseDataService<QuestCommandHistoryEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestCommandHistoryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
