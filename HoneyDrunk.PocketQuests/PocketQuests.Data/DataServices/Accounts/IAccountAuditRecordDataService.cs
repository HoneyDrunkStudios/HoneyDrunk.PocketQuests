using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Persistence operations and entity-specific queries for AccountAuditRecord.</summary>
public interface IAccountAuditRecordDataService : IBaseDataService<AccountAuditRecordEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<AccountAuditRecordEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a canonical shared audit record together with its explicit product ownership link.</summary>
    /// <param name="ownership">Product ownership association.</param>
    /// <param name="audit">Canonical shared record.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task AddWithAuditAsync(AccountAuditRecordEntity ownership, HoneyDrunk.Audit.Data.AuditRecord audit, CancellationToken token = default);
}
