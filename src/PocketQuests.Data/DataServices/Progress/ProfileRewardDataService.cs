using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for ProfileReward.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class ProfileRewardDataService(AppDbContext context) : BaseDataService<ProfileRewardEntity>(context), IProfileRewardDataService
{
    private IQueryable<ProfileRewardEntity> ProfileRewards => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileRewardEntity>> GetCatalog(CancellationToken cancellationToken = default) =>
        await ProfileRewards.ToListAsync(cancellationToken);
}
