using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for QuestOccurrenceRevision.</summary>
public interface IQuestOccurrenceRevisionService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<QuestOccurrenceRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<QuestOccurrenceRevisionEntity> SaveAsync(Guid accountId, QuestOccurrenceRevisionEntity value, CancellationToken cancellationToken = default);

    /// <summary>Resolves the immutable revision referenced by the current occurrence head.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="occurrenceId">Owned occurrence.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The stable current revision identifier.</returns>
    Task<Guid> CurrentRevisionIdAsync(Guid accountId, Guid occurrenceId, CancellationToken token = default);
}
