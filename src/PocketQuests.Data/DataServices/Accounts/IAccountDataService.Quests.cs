using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Operation-specific account and retained-history queries within the shared unit of work.</summary>
public partial interface IAccountDataService
{
    /// <summary>The current private lifecycle barrier.</summary>
    /// <param name="identityUserId">Verified operation query key.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<AccountLifecycleStateEntity?> GetLifecycle(string identityUserId, CancellationToken token);

    /// <summary>The original retained erasure marker.</summary>
    /// <param name="identityUserId">Verified operation query key.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<ErasureMarkerEntity?> GetErasure(string identityUserId, CancellationToken token);

    /// <summary>The globally addressed receipt; Services must verify ownership before use.</summary>
    /// <param name="operationId">Verified operation query key.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<CommandReceiptEntity?> GetReceipt(Guid operationId, CancellationToken token);

    /// <summary>The owned recorded-clock anchor.</summary>
    /// <param name="accountId">Resolved account owner.</param>
    /// <param name="anchorId">Recorded-clock anchor identifier.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<SyncAnchorEntity?> GetAnchor(Guid accountId, Guid anchorId, CancellationToken token);
}
