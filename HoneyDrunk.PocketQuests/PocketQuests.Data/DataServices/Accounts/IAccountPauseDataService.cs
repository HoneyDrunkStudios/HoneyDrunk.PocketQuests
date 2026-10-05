using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Persistence operations and entity-specific queries for AccountPause.</summary>
public interface IAccountPauseDataService : IBaseDataService<AccountPauseEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<AccountPauseEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default);
}
