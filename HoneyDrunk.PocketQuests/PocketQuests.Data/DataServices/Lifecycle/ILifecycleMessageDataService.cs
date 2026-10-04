using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.DataServices.Lifecycle;

/// <summary>Persistence operations and entity-specific queries for LifecycleMessage.</summary>
public interface ILifecycleMessageDataService : IBaseDataService<LifecycleMessageEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<LifecycleMessageEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a shared outbox envelope and its product ownership in the caller transaction.</summary>
    /// <param name="ownership">Explicit owned envelope link.</param>
    /// <param name="message">Shared outbox message.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task AddWithEnvelopeAsync(LifecycleMessageEntity ownership, HoneyDrunk.Data.Outbox.OutboxMessage message, CancellationToken token = default);

    /// <summary>Deletes only owned expired or successfully dispatched acknowledgment envelopes.</summary>
    /// <param name="now">Authoritative retention instant.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the persistence operation.</returns>
    Task DeleteDeliveredOrExpiredAsync(DateTimeOffset now, CancellationToken token = default);
}
