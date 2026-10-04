using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for QuestOccurrence.</summary>
public interface IQuestOccurrenceService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<QuestOccurrenceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<QuestOccurrenceEntity> SaveAsync(Guid accountId, QuestOccurrenceEntity value, CancellationToken cancellationToken = default);

    /// <summary>Appends occurrence revisions and transition events only when effective state changes.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task ApplyOccurrencesAsync(QuestCommit change, CancellationToken token = default);

    /// <summary>Projects a bounded committed occurrence page using only referenced terms.</summary>
    /// <param name="account">Resolved account.</param>
    /// <param name="after">Exclusive creation ordinal.</param>
    /// <param name="size">Requested page size.</param>
    /// <param name="projectionAt">Authoritative projection time.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The requested source-derived result.</returns>
    Task<QuestOccurrencePage> ReadPageAsync(AccountEntity account, int after, int size, DateTimeOffset projectionAt, CancellationToken token = default);
}
