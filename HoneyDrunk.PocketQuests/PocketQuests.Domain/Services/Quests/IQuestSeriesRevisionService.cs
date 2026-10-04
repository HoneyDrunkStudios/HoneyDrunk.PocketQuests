using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for QuestSeriesRevision.</summary>
public interface IQuestSeriesRevisionService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<QuestSeriesRevisionEntity> SaveAsync(Guid accountId, QuestSeriesRevisionEntity value, CancellationToken cancellationToken = default);

    /// <summary>Reads configurations already stored before the current command's staged edits.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Committed immutable configurations.</returns>
    Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetCommittedByAccountIdAsync(Guid accountId, CancellationToken token = default);
}
