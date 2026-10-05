using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for TimeZoneChange.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class TimeZoneChangeDataService(AppDbContext context) : BaseDataService<TimeZoneChangeEntity>(context), ITimeZoneChangeDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TimeZoneChangeEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
