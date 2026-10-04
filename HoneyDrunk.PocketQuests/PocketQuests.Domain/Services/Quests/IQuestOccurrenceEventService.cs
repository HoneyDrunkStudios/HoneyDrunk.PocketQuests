using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for QuestOccurrenceEvent.</summary>
public interface IQuestOccurrenceEventService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<QuestOccurrenceEventEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<QuestOccurrenceEventEntity> SaveAsync(Guid accountId, QuestOccurrenceEventEntity value, CancellationToken cancellationToken = default);

    /// <summary>Appends one stable occurrence event without rewriting an existing event.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="id">Stable event identifier.</param>
    /// <param name="occurrenceId">Owned occurrence.</param>
    /// <param name="code">Transition discriminator.</param>
    /// <param name="at">Effective instant.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task AppendAsync(QuestCommit change, Guid id, Guid occurrenceId, string code, DateTimeOffset at, CancellationToken token = default);
}
