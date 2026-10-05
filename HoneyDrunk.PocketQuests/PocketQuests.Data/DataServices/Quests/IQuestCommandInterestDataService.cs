using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestCommandInterest.</summary>
public interface IQuestCommandInterestDataService : IBaseDataService<QuestCommandInterestEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestCommandInterestEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default);
}
