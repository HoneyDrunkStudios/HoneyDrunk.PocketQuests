using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Persistence operations and entity-specific queries for Account.</summary>
public interface IAccountDataService : IBaseDataService<AccountEntity>
{
    /// <summary>Gets the account for the verified canonical Identity user.</summary>
    /// <param name="identityUserId">Verified canonical Identity user identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked account, or null when no profile exists.</returns>
    Task<AccountEntity?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken = default);

    /// <summary>Reads the owned source rows used by the business state projection.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Relational query results without business model dependencies.</returns>
    Task<AccountStateData> GetQuestStateAsync(Guid accountId, CancellationToken token = default);

    /// <summary>Serializes commands and verified lifecycle transitions using the existing SQL transaction lock resource.</summary>
    /// <param name="identityUserId">Verified canonical Identity user.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task AcquireCommandLockAsync(string identityUserId, CancellationToken token = default);

    /// <summary>Reads a bounded maintenance keyset page without tracking or writing accounts.</summary>
    /// <param name="now">Maintenance instant.</param>
    /// <param name="afterProjectionAt">Prior cursor instant.</param>
    /// <param name="afterAccountId">Prior cursor account.</param>
    /// <param name="maximumAccounts">Maximum page size.</param>
    /// <param name="activeState">Authoritative active state discriminator.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Candidate accounts including one lookahead row.</returns>
    Task<IReadOnlyList<AccountEntity>> GetReconciliationCandidatesAsync(DateTimeOffset now, DateTimeOffset? afterProjectionAt, Guid? afterAccountId, int maximumAccounts, string activeState, CancellationToken token = default);

    /// <summary>Checks indexed recurring deliveries and expired active occurrences.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="date">Local calendar date.</param>
    /// <param name="at">Projection instant.</param>
    /// <param name="includeSeries">Whether the account can deliver recurring offers.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Whether persisted state needs reconciliation.</returns>
    Task<bool> HasDueWorkAsync(Guid accountId, DateOnly date, DateTimeOffset at, bool includeSeries, CancellationToken token = default);

    /// <summary>Deletes only verified account-owned rows and explicitly linked shared records in FK order.</summary>
    /// <param name="identityUserId">Verified canonical user under the account lock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task DeleteOwnedAsync(string identityUserId, CancellationToken token = default);
}
