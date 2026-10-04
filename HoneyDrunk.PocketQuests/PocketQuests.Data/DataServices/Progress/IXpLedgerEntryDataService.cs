using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>Persistence operations and entity-specific queries for XpLedgerEntry.</summary>
public interface IXpLedgerEntryDataService : IBaseDataService<XpLedgerEntryEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<XpLedgerEntryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
