using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for QuestCommandHistory.</summary>
public interface IQuestCommandHistoryService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<QuestCommandHistoryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<QuestCommandHistoryEntity> SaveAsync(Guid accountId, QuestCommandHistoryEntity value, CancellationToken cancellationToken = default);

    /// <summary>Retains typed successful inputs and replay budgets without storing an account snapshot.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task AppendAsync(QuestCommit change, CancellationToken token = default);

    /// <summary>Replays exact retained inputs under their original clock, rules and reconciliation budgets.</summary>
    /// <param name="account">Resolved owned account.</param>
    /// <param name="version">Last visible committed mutation.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task<QuestReplay> ReplayAsync(AccountEntity account, long version, CancellationToken token = default);
}
