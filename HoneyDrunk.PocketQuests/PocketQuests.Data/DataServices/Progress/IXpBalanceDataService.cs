using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>Persistence operations and entity-specific queries for XpBalance.</summary>
public interface IXpBalanceDataService : IBaseDataService<XpBalanceEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<XpBalanceEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default);
}
