using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.DataServices.Lifecycle;

/// <summary>EF persistence and queries for ErasureMarker.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class ErasureMarkerDataService(AppDbContext context) : BaseDataService<ErasureMarkerEntity>(context), IErasureMarkerDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ErasureMarkerEntity>> GetByIdentityUserId(string identityUserId, CancellationToken cancellationToken = default) =>
        await DbSet.Where(row => row.Id == identityUserId).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task DeleteExpired(DateTimeOffset cutoff, CancellationToken token = default)
    {
        await DbSet.Where(marker => marker.CreatedAt <= cutoff).ExecuteDeleteAsync(token);
    }
}
