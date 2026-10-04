using PocketQuests.Data.Entities.Progress;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Progress;

/// <summary>Business access and invariants for XpLedgerEntry.</summary>
public interface IXpLedgerEntryService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<XpLedgerEntryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<XpLedgerEntryEntity> SaveAsync(Guid accountId, XpLedgerEntryEntity value, CancellationToken cancellationToken = default);

    /// <summary>Recalculates ledger contributions while retaining their immutable source events.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task RecalculateAsync(QuestCommit change, CancellationToken token = default);
}
