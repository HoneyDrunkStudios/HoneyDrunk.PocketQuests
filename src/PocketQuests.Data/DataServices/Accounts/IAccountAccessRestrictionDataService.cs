using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Administration;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Current account restrictions and bounded product-directory queries.</summary>
public interface IAccountAccessRestrictionDataService : IBaseDataService<AccountAccessRestrictionEntity>
{
    /// <summary>Reads the tracked moderation row under its account lock.</summary>
    /// <param name="accountId">Product account identifier.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current row, or null before the first access change.</returns>
    Task<AccountAccessRestrictionEntity?> GetForAccount(Guid accountId, CancellationToken token);

    /// <summary>Reads at most the supplied limit in stable canonical-user order, excluding erased profiles.</summary>
    /// <param name="query">Bounded literal name or user-identifier search.</param>
    /// <param name="after">Exclusive canonical-user cursor.</param>
    /// <param name="limit">Maximum selected row count, including lookahead.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Selected directory metadata in canonical-user order.</returns>
    Task<IReadOnlyList<AdminUserRow>> Search(string query, string? after, int limit, CancellationToken token);
}
