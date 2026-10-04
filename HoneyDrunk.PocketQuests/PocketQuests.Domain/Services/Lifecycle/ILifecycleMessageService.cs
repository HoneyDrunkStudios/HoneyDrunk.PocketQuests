using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Domain.Services.Lifecycle;

/// <summary>Business access and invariants for LifecycleMessage.</summary>
public interface ILifecycleMessageService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<LifecycleMessageEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<LifecycleMessageEntity> SaveAsync(LifecycleMessageEntity value, CancellationToken cancellationToken = default);

    /// <summary>Stages an idempotent private acknowledgment with explicit shared-envelope ownership.</summary>
    /// <param name="intent">Verified lifecycle instruction.</param>
    /// <param name="queue">Trusted acknowledgment destination.</param>
    /// <param name="now">Authoritative host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged acknowledgment.</returns>
    Task AcknowledgeAsync(LifecycleIntent intent, string queue, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Prunes owned dispatched or expired acknowledgment envelopes.</summary>
    /// <param name="now">Authoritative retention clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of retention cleanup.</returns>
    Task PruneAsync(DateTimeOffset now, CancellationToken token = default);
}
