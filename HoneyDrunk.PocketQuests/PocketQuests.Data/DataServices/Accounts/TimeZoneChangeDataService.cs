using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for TimeZoneChange.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class TimeZoneChangeDataService(AppDbContext context) : BaseDataService<TimeZoneChangeEntity>(context), ITimeZoneChangeDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TimeZoneChangeEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
