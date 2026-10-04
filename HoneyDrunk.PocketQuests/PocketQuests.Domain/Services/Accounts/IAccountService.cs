using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Business access and invariants for Account.</summary>
public interface IAccountService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="identityUserId">Verified canonical Identity identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<AccountEntity?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<AccountEntity> SaveAsync(AccountEntity value, CancellationToken cancellationToken = default);

    /// <summary>Applies validated profile state and its owned source history.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task ApplyProfileAsync(QuestCommit change, CancellationToken token = default);

    /// <summary>Constructs current business state from owned relational query results.</summary>
    /// <param name="account">Resolved owned account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task<QuestAggregate> LoadCurrentAsync(AccountEntity account, CancellationToken token = default);
}
