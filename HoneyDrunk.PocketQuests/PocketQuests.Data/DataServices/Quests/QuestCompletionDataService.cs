using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Accounts;
using PocketQuests.Data.Queries.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for QuestCompletion.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestCompletionDataService(AppDbContext context) : BaseDataService<QuestCompletionEntity>(context), IQuestCompletionDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCompletionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestCompletionEntity>> GetCurrentForOccurrencesAsync(Guid accountId, Guid[] ids, DateTimeOffset at, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.QuestOccurrenceId) && row.UndoneAt == null && row.RecordedAt <= at).ToListAsync(token);
    }

    /// <inheritdoc />
    public Task AcquireAccountLock(string identityUserId, CancellationToken token) => AccountQueries.AcquireLock(Context, identityUserId, token);

    /// <inheritdoc />
    public Task<AccountEntity?> GetAccount(string identityUserId, CancellationToken token) => Context.Account.SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, token);

    /// <inheritdoc />
    public Task<AccountLifecycleStateEntity?> GetLifecycle(string identityUserId, CancellationToken token) => Context.AccountLifecycleState.SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, token);

    /// <inheritdoc />
    public Task<ErasureMarkerEntity?> GetErasure(string identityUserId, CancellationToken token) => Context.ErasureMarker.SingleOrDefaultAsync(row => row.Id == identityUserId, token);

    /// <inheritdoc />
    public Task<CommandReceiptEntity?> GetReceipt(Guid operationId, CancellationToken token) => Context.CommandReceipt.SingleOrDefaultAsync(row => row.Id == operationId, token);

    /// <inheritdoc />
    public Task<SyncAnchorEntity?> GetAnchor(Guid accountId, Guid anchorId, CancellationToken token) => Context.SyncAnchor.SingleOrDefaultAsync(row => row.AccountId == accountId && row.Id == anchorId, token);

    /// <inheritdoc />
    public Task<QuestCompletionRows> GetCompletionRows(Guid accountId, long version, CancellationToken token) => QuestCompletionQueries.Read(Context, accountId, version, token);

    /// <inheritdoc />
    public void Apply(QuestCompletionChanges changes)
    {
        Context.QuestOccurrence.AddRange(changes.Occurrences);
        Context.QuestOccurrenceRevision.AddRange(changes.Revisions);
        Context.QuestOccurrenceEvent.AddRange(changes.Events);
        Context.QuestCompletion.AddRange(changes.Completions);
        Context.XpLedgerEntry.AddRange(changes.Ledger);
        Context.XpLedgerEntry.RemoveRange(changes.RemovedLedger);
        Context.XpBalance.AddRange(changes.Balances);
        Context.XpBalance.RemoveRange(changes.RemovedBalances);
        Context.CategoryProgress.AddRange(changes.Categories);
        Context.CategoryProgress.RemoveRange(changes.RemovedCategories);
        Context.AccountEntitlement.AddRange(changes.Entitlements);
        Context.AccountEntitlement.RemoveRange(changes.RemovedEntitlements);
        Context.QuestCommandHistory.Add(changes.History);
        if (changes.Receipt is not null)
            Context.CommandReceipt.Add(changes.Receipt);
        Context.Add(changes.Audit);
        Context.AccountAuditRecord.Add(changes.AuditOwnership);
    }
}
