using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestOccurrence.</summary>
public interface IQuestOccurrenceDataService : IBaseDataService<QuestOccurrenceEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestOccurrenceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded occurrence keyset page including a lookahead row.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="after">Exclusive creation ordinal.</param>
    /// <param name="size">Page size.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Committed rows in creation order.</returns>
    Task<IReadOnlyList<QuestOccurrenceEntity>> GetPageAsync(Guid accountId, int after, int size, CancellationToken token = default);
}
