using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.DataServices.Lifecycle;

/// <summary>Persistence operations and entity-specific queries for ErasureMarker.</summary>
public interface IErasureMarkerDataService : IBaseDataService<ErasureMarkerEntity>
{
    /// <summary>Gets the retained marker for the canonical Identity user.</summary>
    /// <param name="identityUserId">Verified canonical Identity user identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<ErasureMarkerEntity>> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken = default);

    /// <summary>Deletes minimal markers whose original erasure instant is outside retention.</summary>
    /// <param name="cutoff">Inclusive original erasure cutoff.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken token = default);
}
