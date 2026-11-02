using PocketQuests.Data.Entities.Discoveries;

namespace PocketQuests.Data.DataServices.Discoveries;

/// <summary>Bounded account-owned discovery persistence.</summary>
public interface ISavedPlaceDataService : IBaseDataService<SavedPlaceEntity>
{
    /// <summary>Reads at most 100 tracked places belonging to this account.</summary>
    /// <param name="accountId">Trusted owner.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Bounded matching rows.</returns>
    Task<IReadOnlyList<SavedPlaceEntity>> GetByAccountId(Guid accountId, CancellationToken token = default);

    /// <summary>Checks that a linked occurrence belongs to the same account.</summary>
    /// <param name="accountId">Trusted owner.</param>
    /// <param name="occurrenceId">Accepted quest identifier.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Whether the same owner has this quest.</returns>
    Task<bool> CanLink(Guid accountId, Guid occurrenceId, CancellationToken token = default);
}
