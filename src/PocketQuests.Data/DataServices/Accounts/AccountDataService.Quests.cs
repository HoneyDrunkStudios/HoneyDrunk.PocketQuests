using Microsoft.EntityFrameworkCore;
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
}
