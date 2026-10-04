using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestCompletion.</summary>
public interface IQuestCompletionDataService : IBaseDataService<QuestCompletionEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestCompletionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Reads current completions for the selected occurrence page.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="ids">Occurrence identifiers.</param>
    /// <param name="at">Projection instant.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Current committed completions.</returns>
    Task<IReadOnlyList<QuestCompletionEntity>> GetCurrentForOccurrencesAsync(Guid accountId, Guid[] ids, DateTimeOffset at, CancellationToken token = default);
}
