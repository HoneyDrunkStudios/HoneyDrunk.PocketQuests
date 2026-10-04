using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Domain.Services.Lifecycle;

/// <summary>Business access and invariants for ErasureMarker.</summary>
public interface IErasureMarkerService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="identityUserId">Verified canonical Identity identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<ErasureMarkerEntity>> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<ErasureMarkerEntity> SaveAsync(ErasureMarkerEntity value, CancellationToken cancellationToken = default);

    /// <summary>Erases verified owned data and retains the original minimal marker without resetting its clock.</summary>
    /// <param name="identityUserId">Verified canonical user under the account lock.</param>
    /// <param name="originalErasedAt">Original verified live-erasure instant.</param>
    /// <param name="now">Authoritative host time.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged marker and owned purge.</returns>
    Task PurgeAsync(string identityUserId, DateTimeOffset originalErasedAt, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Removes markers exactly thirty-five elapsed days after their original erasure.</summary>
    /// <param name="now">Authoritative retention instant.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of retention cleanup.</returns>
    Task PruneAsync(DateTimeOffset now, CancellationToken token = default);
}
