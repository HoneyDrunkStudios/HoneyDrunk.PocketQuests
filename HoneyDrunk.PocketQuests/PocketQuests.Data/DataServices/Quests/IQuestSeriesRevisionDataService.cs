using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestSeriesRevision.</summary>
public interface IQuestSeriesRevisionDataService : IBaseDataService<QuestSeriesRevisionEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Reads configurations already stored before the current command's staged edits.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Committed immutable configurations.</returns>
    Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetCommittedByAccountIdAsync(Guid accountId, CancellationToken token = default);

    /// <summary>Reads only account-owned rows referenced by a bounded query.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="ids">Referenced identifiers.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Matching committed rows.</returns>
    Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetSelectedAsync(Guid accountId, Guid[] ids, CancellationToken token = default);
}
