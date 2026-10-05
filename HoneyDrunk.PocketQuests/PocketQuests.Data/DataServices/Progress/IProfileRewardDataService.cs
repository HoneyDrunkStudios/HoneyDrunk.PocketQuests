using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>Persistence operations and entity-specific queries for ProfileReward.</summary>
public interface IProfileRewardDataService : IBaseDataService<ProfileRewardEntity>
{
    /// <summary>Gets the versioned public catalog.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<ProfileRewardEntity>> GetCatalog(CancellationToken cancellationToken = default);
}
