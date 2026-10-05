using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Synchronization;

/// <summary>Persistence operations and entity-specific queries for CommandReceipt.</summary>
public interface ICommandReceiptDataService : IBaseDataService<CommandReceiptEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<CommandReceiptEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default);
}
