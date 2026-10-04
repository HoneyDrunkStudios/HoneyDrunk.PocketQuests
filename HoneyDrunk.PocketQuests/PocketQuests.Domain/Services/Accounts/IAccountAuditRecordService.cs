using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Business access and invariants for AccountAuditRecord.</summary>
public interface IAccountAuditRecordService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<AccountAuditRecordEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<AccountAuditRecordEntity> SaveAsync(Guid accountId, AccountAuditRecordEntity value, CancellationToken cancellationToken = default);

    /// <summary>Stages shared audit content and its product ownership for one committed command.</summary>
    /// <param name="identity">Verified Identity actor.</param>
    /// <param name="change">The transaction's validated command.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged audit.</returns>
    Task RecordCommandAsync(AccountIdentity identity, QuestCommit change, CancellationToken token = default);
}
