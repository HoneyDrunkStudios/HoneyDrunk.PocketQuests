using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestOccurrenceRevision.</summary>
public interface IQuestOccurrenceRevisionDataService : IBaseDataService<QuestOccurrenceRevisionEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestOccurrenceRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Reads only account-owned rows referenced by a bounded query.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="ids">Referenced identifiers.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Matching committed rows.</returns>
    Task<IReadOnlyList<QuestOccurrenceRevisionEntity>> GetSelectedAsync(Guid accountId, Guid[] ids, CancellationToken token = default);
}
