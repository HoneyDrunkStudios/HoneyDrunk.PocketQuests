using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Domain.Services.Synchronization;

/// <summary>Business access and invariants for SyncAnchor.</summary>
public interface ISyncAnchorService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<SyncAnchorEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<SyncAnchorEntity> SaveAsync(Guid accountId, SyncAnchorEntity value, CancellationToken cancellationToken = default);
}
