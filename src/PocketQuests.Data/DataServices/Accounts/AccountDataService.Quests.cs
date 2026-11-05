using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Account lifecycle, receipt and anchor queries for the current unit of work.</summary>
public sealed partial class AccountDataService
{
    /// <inheritdoc />
    public Task<AccountLifecycleStateEntity?> GetLifecycle(string identityUserId, CancellationToken token) => Context.AccountLifecycleState.SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, token);

    /// <inheritdoc />
    public Task<ErasureMarkerEntity?> GetErasure(string identityUserId, CancellationToken token) => Context.ErasureMarker.SingleOrDefaultAsync(row => row.Id == identityUserId, token);

    /// <inheritdoc />
    public Task<CommandReceiptEntity?> GetReceipt(Guid operationId, CancellationToken token) => Context.CommandReceipt.SingleOrDefaultAsync(row => row.Id == operationId, token);

    /// <inheritdoc />
    public Task<SyncAnchorEntity?> GetAnchor(Guid accountId, Guid anchorId, CancellationToken token) => Context.SyncAnchor.SingleOrDefaultAsync(row => row.AccountId == accountId && row.Id == anchorId, token);
}
