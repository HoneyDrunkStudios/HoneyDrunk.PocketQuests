using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Domain.Services.Lifecycle;

/// <summary>Business access and invariants for AccountLifecycleState.</summary>
public interface IAccountLifecycleStateService : IQuestLifecycle
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<AccountLifecycleStateEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<AccountLifecycleStateEntity> SaveAsync(AccountLifecycleStateEntity value, CancellationToken cancellationToken = default);

    /// <summary>Fences stale resolution and durably freezes recovered progress until the existing explicit resume command.</summary>
    /// <param name="user">Current authoritative Identity response, never a public request body.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion once any new lifecycle version is durable.</returns>
    Task ObserveActive(UserRecord user, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Consumes a verified private Identity instruction and stages its capability acknowledgment atomically.</summary>
    /// <param name="intent">Instruction from the authorized private consumer endpoint.</param>
    /// <param name="acknowledgmentQueue">Trusted host configuration.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion only after transition and outbox ownership commit.</returns>
    Task ReceiveLifecycle(LifecycleIntent intent, string acknowledgmentQueue, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Reapplies external verified erasure evidence while the restored database remains closed to product traffic.</summary>
    /// <param name="identityUserId">Canonical marker identifier from the verified external source.</param>
    /// <param name="originalErasedAt">Original verified live-erasure time; never the restore/retry clock.</param>
    /// <param name="now">Restore host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion after scoped purge and unchanged original marker insertion.</returns>
    Task ReapplyErasure(string identityUserId, DateTimeOffset originalErasedAt, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Removes only owned dispatched/expired acknowledgments and markers at their original thirty-five-day boundary.</summary>
    /// <param name="now">Retention host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the atomic owned-envelope and marker cleanup.</returns>
    Task PruneLifecycle(DateTimeOffset now, CancellationToken token = default);
}
