using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Domain.Services.Progress;

/// <summary>Business access and invariants for ProfileReward.</summary>
public interface IProfileRewardService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<ProfileRewardEntity>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
