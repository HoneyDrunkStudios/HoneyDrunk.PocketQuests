using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Persistence operations and entity-specific queries for TimeZoneChange.</summary>
public interface ITimeZoneChangeDataService : IBaseDataService<TimeZoneChangeEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<TimeZoneChangeEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default);
}
