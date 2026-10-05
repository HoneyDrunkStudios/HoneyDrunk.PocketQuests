using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for QuestCompletion.</summary>
public interface IQuestCompletionDataService : IBaseDataService<QuestCompletionEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<QuestCompletionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Reads current completions for the selected occurrence page.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="ids">Occurrence identifiers.</param>
    /// <param name="at">Projection instant.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Current committed completions.</returns>
    Task<IReadOnlyList<QuestCompletionEntity>> GetCurrentForOccurrencesAsync(Guid accountId, Guid[] ids, DateTimeOffset at, CancellationToken token = default);

    /// <summary>Acquires the transaction-owned lock before checking account state.</summary>
    /// <param name="identityUserId">Trusted canonical Identity user.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion when serialization is acquired.</returns>
    Task AcquireAccountLock(string identityUserId, CancellationToken token);

    /// <summary>Reads the resolved account entity.</summary>
    /// <param name="identityUserId">Trusted canonical Identity user.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The owned account or null.</returns>
    Task<AccountEntity?> GetAccount(string identityUserId, CancellationToken token);

    /// <summary>Reads the private lifecycle fence.</summary>
    /// <param name="identityUserId">Trusted canonical Identity user.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current fence or null.</returns>
    Task<AccountLifecycleStateEntity?> GetLifecycle(string identityUserId, CancellationToken token);

    /// <summary>Reads an original verified erasure marker.</summary>
    /// <param name="identityUserId">Trusted canonical Identity user.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The marker or null.</returns>
    Task<ErasureMarkerEntity?> GetErasure(string identityUserId, CancellationToken token);

    /// <summary>Reads an operation receipt, including one belonging to another account.</summary>
    /// <param name="operationId">Client operation ID.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The persisted receipt or null; the service validates ownership.</returns>
    Task<CommandReceiptEntity?> GetReceipt(Guid operationId, CancellationToken token);

    /// <summary>Reads an owned offline time anchor.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="anchorId">Supplied proof anchor.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The owned anchor or null.</returns>
    Task<SyncAnchorEntity?> GetAnchor(Guid accountId, Guid anchorId, CancellationToken token);

    /// <summary>Reads owned entity collections once, with source history through the requested version.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="version">Inclusive source history version.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Owned entity collections in query-defined order.</returns>
    Task<QuestCompletionRows> GetCompletionRows(Guid accountId, long version, CancellationToken token);

    /// <summary>Stages only supplied new/deleted entities; existing entities are tracked.</summary>
    /// <param name="changes">The service's entity changes.</param>
    void Apply(QuestCompletionChanges changes);
}
