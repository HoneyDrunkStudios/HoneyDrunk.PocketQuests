using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for Account.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountDataService(AppDbContext context) : BaseDataService<AccountEntity>(context), IAccountDataService
{
    /// <inheritdoc />
    public Task<AccountEntity?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken = default) =>
        DbSet.SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, cancellationToken);

    /// <inheritdoc />
    public Task<AccountStateData> GetQuestStateAsync(Guid accountId, CancellationToken token = default) =>
        AccountQueries.ReadStateAsync(Context, accountId, token);

    /// <inheritdoc />
    public Task AcquireCommandLockAsync(string identityUserId, CancellationToken token = default) => AccountQueries.AcquireLock(Context, identityUserId, token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountEntity>> GetReconciliationCandidatesAsync(DateTimeOffset now, DateTimeOffset? afterProjectionAt, Guid? afterAccountId, int maximumAccounts, string activeState, CancellationToken token = default)
    {
        var query = DbSet.AsNoTracking().Where(account => account.ProjectionAsOfAt < now && account.MutationVersion > 0
            && !Context.ErasureMarker.Any(marker => marker.Id == account.IdentityUserId)
            && !Context.AccountLifecycleState.Any(barrier => barrier.IdentityUserId == account.IdentityUserId && barrier.StateCode != activeState));
        if (afterProjectionAt is not null && afterAccountId is not null)
            query = query.Where(account => account.ProjectionAsOfAt > afterProjectionAt || (account.ProjectionAsOfAt == afterProjectionAt && account.Id.CompareTo(afterAccountId.Value) > 0));
        return await query.OrderBy(account => account.ProjectionAsOfAt).ThenBy(account => account.Id).Take(maximumAccounts + 1).ToListAsync(token);
    }

    /// <inheritdoc />
    public async Task<bool> HasDueWorkAsync(Guid accountId, DateOnly date, DateTimeOffset at, bool includeSeries, CancellationToken token = default)
    {
        return (includeSeries && await Context.QuestSeries.AnyAsync(series => series.AccountId == accountId && series.StoppedAt == null && series.NextDeliveryOn <= date, token))
            || await Context.QuestOccurrence.AnyAsync(occurrence => occurrence.AccountId == accountId && occurrence.StateCode == "Active" && occurrence.DeadlineAt <= at, token);
    }

    /// <inheritdoc />
    public async Task DeleteOwnedAsync(string identityUserId, CancellationToken token = default)
    {
        if (Context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Erasure requires an explicit verified lifecycle transaction.");
        if (Context.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Erasure cannot discard staged changes from another operation.");
        var accountId = await DbSet.AsNoTracking().Where(account => account.IdentityUserId == identityUserId).Select(account => (Guid?)account.Id).SingleOrDefaultAsync(token);
        var auditIds = await Context.AccountAuditRecord.Where(link => link.AccountId == accountId).Select(link => link.AuditRecordId).ToArrayAsync(token);
        await Context.AccountAuditRecord.Where(link => link.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.Set<HoneyDrunk.Audit.Data.AuditRecord>().Where(audit => auditIds.Contains(audit.Id)).ExecuteDeleteAsync(token);
        var outboxIds = await Context.LifecycleMessage.Where(link => link.IdentityUserId == identityUserId).Select(link => link.OutboxMessageId).ToArrayAsync(token);
        await Context.LifecycleMessage.Where(link => link.IdentityUserId == identityUserId).ExecuteDeleteAsync(token);
        await Context.Set<HoneyDrunk.Data.Outbox.OutboxMessage>().Where(message => outboxIds.Contains(message.Id)).ExecuteDeleteAsync(token);
        await Context.AccountLifecycleState.Where(barrier => barrier.IdentityUserId == identityUserId).ExecuteDeleteAsync(token);
        await Context.QuestOccurrence.Where(row => row.AccountId == accountId).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ParentQuestOccurrenceId, (Guid?)null), token);
        await Context.AccountEntitlement.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.AccountInterest.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.AccountPause.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.CategoryProgress.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestCommandInterest.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestDefinitionAttributeAllocation.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestDefinitionSkillAllocation.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.SkillAssessment.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.TimeZoneChange.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.XpBalance.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.XpLedgerEntry.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestCommandHistory.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.CustomSkill.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestCompletion.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestOccurrenceEvent.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestOccurrenceRevision.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestOccurrence.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestSeriesRevision.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.SyncAnchor.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestDefinitionRevision.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestSeries.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.CommandReceipt.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await Context.QuestDefinition.Where(row => row.AccountId == accountId).ExecuteDeleteAsync(token);
        await DbSet.Where(account => account.Id == accountId).ExecuteDeleteAsync(token);

        // Bulk erasure bypasses tracking. The precondition above guarantees this only drops
        // unchanged rows read by the erasure workflow, never another operation's staged work.
        Context.ChangeTracker.Clear();
    }
}
