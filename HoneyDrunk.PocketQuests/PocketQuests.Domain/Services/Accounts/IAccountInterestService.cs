using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Business access and invariants for AccountInterest.</summary>
public interface IAccountInterestService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<AccountInterestEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<AccountInterestEntity> SaveAsync(Guid accountId, AccountInterestEntity value, CancellationToken cancellationToken = default);

    /// <summary>Stages ordered current interests and removes deselected associations.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task ApplyInterestsAsync(QuestCommit change, CancellationToken token = default);
}
