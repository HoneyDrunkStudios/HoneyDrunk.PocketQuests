using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Account queries and explicit quest changes for the current unit of work.</summary>
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

    /// <inheritdoc />
    public Task<QuestStateRows> ReadCurrentState(Guid accountId, CancellationToken token) => QuestStateQueries.Read(Context, accountId, token);

    /// <inheritdoc />
    public Task<QuestReplayRows> ReadReplay(Guid accountId, long version, CancellationToken token) => QuestReplayQueries.Read(Context, accountId, version, token);

    /// <inheritdoc />
    public Task<QuestProjectionRows> ReadProjections(Guid accountId, CancellationToken token) => QuestProjectionQueries.Read(Context, accountId, token);

    /// <inheritdoc />
    public Task<QuestTermsRows> ReadTerms(Guid accountId, Guid[] definitionIds, CancellationToken token) => QuestTermsQueries.Read(Context, accountId, definitionIds, token);

    /// <inheritdoc />
    public Task<QuestTermsRows> ReadSelectedTerms(Guid accountId, Guid[] revisionIds, CancellationToken token) => QuestTermsQueries.ReadSelected(Context, accountId, revisionIds, token);

    /// <inheritdoc />
    public async Task<QuestDefinitionHistoryRows> ReadDefinitionHistory(Guid accountId, CancellationToken token)
    {
        var definitionIds = await Context.QuestDefinition.AsNoTracking().Where(row => row.AccountId == accountId && row.SystemQuestId == null).Select(row => row.Id).ToArrayAsync(token);
        var terms = await QuestTermsQueries.Read(Context, accountId, definitionIds, token);
        var archived = await Context.QuestCommandHistory.AsNoTracking().Where(row => row.AccountId == accountId && row.ActionCode == "archive-definition" && row.QuestDefinitionRevisionId != null)
            .Select(row => row.QuestDefinitionRevisionId!.Value).ToListAsync(token);
        return new(terms, archived);
    }

    /// <inheritdoc />
    public void Apply(QuestChanges changes)
    {
        if (Context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Quest changes require an active operation transaction.");
        Context.QuestDefinition.AddRange(changes.Definitions);
        Context.QuestDefinitionRevision.AddRange(changes.DefinitionRevisions);
        Context.QuestDefinitionAttributeAllocation.AddRange(changes.DefinitionAttributes);
        Context.QuestDefinitionSkillAllocation.AddRange(changes.DefinitionSkills);
        Context.QuestSeries.AddRange(changes.Series);
        Context.QuestSeriesRevision.AddRange(changes.SeriesRevisions);
        Context.CustomSkill.AddRange(changes.Skills);
        Context.AccountInterest.AddRange(changes.Interests);
        Context.AccountInterest.RemoveRange(changes.RemovedInterests);
        Context.AccountPause.AddRange(changes.Pauses);
        Context.SkillAssessment.AddRange(changes.Assessments);
        Context.TimeZoneChange.AddRange(changes.Zones);
        Context.QuestCommandInterest.AddRange(changes.CommandInterests);
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
