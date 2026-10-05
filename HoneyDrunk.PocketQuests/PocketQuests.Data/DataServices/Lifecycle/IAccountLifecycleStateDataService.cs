using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.DataServices.Lifecycle;

/// <summary>Persistence operations and entity-specific queries for AccountLifecycleState.</summary>
public interface IAccountLifecycleStateDataService : IBaseDataService<AccountLifecycleStateEntity>
{
    /// <summary>Reads a fresh untracked fence before deciding whether a lifecycle write needs the account lock.</summary>
    /// <param name="identityUserId">Canonical Identity user identifier.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current observed fence.</returns>
    Task<AccountLifecycleStateEntity?> ReadCurrent(string identityUserId, CancellationToken token = default);

    /// <summary>Reads the unique lifecycle fence for a verified Identity user.</summary>
    /// <param name="identityUserId">Canonical Identity user identifier.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The tracked fence or null.</returns>
    Task<AccountLifecycleStateEntity?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken token = default);

    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<AccountLifecycleStateEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
