using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestSeries.</summary>
public interface IQuestSeriesDataService : IBaseDataService<QuestSeriesEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestSeriesEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default);
}
