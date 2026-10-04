using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Business access and invariants for AccountPause.</summary>
public interface IAccountPauseService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<AccountPauseEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<AccountPauseEntity> SaveAsync(Guid accountId, AccountPauseEntity value, CancellationToken cancellationToken = default);

    /// <summary>Stages this entity's owned profile history from the validated command.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task ApplyPausesAsync(QuestCommit change, CancellationToken token = default);
}
