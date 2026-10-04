using PocketQuests.Data.DataServices.Progress;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Domain.Services.Progress;

/// <summary>Retains ProfileReward ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class ProfileRewardService(IProfileRewardDataService data) : IProfileRewardService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ProfileRewardEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        data.GetCatalogAsync(cancellationToken);
}
