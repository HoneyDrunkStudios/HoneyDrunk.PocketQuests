using PocketQuests.Data.Entities.Progress;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Progress;

/// <summary>Business access and invariants for XpBalance.</summary>
public interface IXpBalanceService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<XpBalanceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<XpBalanceEntity> SaveAsync(Guid accountId, XpBalanceEntity value, CancellationToken cancellationToken = default);

    /// <summary>Recalculates earned and seed balances from the validated projection.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task RecalculateAsync(QuestCommit change, CancellationToken token = default);
}
