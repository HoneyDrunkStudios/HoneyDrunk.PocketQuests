using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Reads account lifecycle, replay and quest state on the shared transaction context.</summary>
/// <param name="context">The scoped context shared with account commands.</param>
public sealed class AccountStateDataService(AppDbContext context) : IAccountStateDataService
{
    /// <inheritdoc />
    public Task<AccountLifecycleStateEntity?> GetLifecycle(string identityUserId, CancellationToken token) => context.AccountLifecycleState.SingleOrDefaultAsync(row => row.IdentityUserId == identityUserId, token);

    /// <inheritdoc />
    public Task<ErasureMarkerEntity?> GetErasure(string identityUserId, CancellationToken token) => context.ErasureMarker.SingleOrDefaultAsync(row => row.Id == identityUserId, token);

    /// <inheritdoc />
    public Task<CommandReceiptEntity?> GetReceipt(Guid operationId, CancellationToken token) => context.CommandReceipt.SingleOrDefaultAsync(row => row.Id == operationId, token);

    /// <inheritdoc />
    public Task<SyncAnchorEntity?> GetAnchor(Guid accountId, Guid anchorId, CancellationToken token) => context.SyncAnchor.SingleOrDefaultAsync(row => row.AccountId == accountId && row.Id == anchorId, token);

    /// <inheritdoc />
    public Task<QuestStateRows> ReadCurrentState(Guid accountId, CancellationToken token) => QuestStateDataService.Read(context, accountId, token);

    /// <inheritdoc />
    public Task<QuestReplayRows> ReadReplay(Guid accountId, long version, CancellationToken token) => QuestReplayDataService.Read(context, accountId, version, token);

    /// <inheritdoc />
    public Task<QuestProjectionRows> ReadProjections(Guid accountId, CancellationToken token) => QuestProjectionDataService.Read(context, accountId, token);

    /// <inheritdoc />
    public Task<QuestTermsRows> ReadTerms(Guid accountId, Guid[] definitionIds, CancellationToken token) => QuestTermsDataService.Read(context, accountId, definitionIds, token);

    /// <inheritdoc />
    public Task<QuestTermsRows> ReadSelectedTerms(Guid accountId, Guid[] revisionIds, CancellationToken token) => QuestTermsDataService.ReadSelected(context, accountId, revisionIds, token);

    /// <inheritdoc />
    public async Task<QuestDefinitionHistoryRows> ReadDefinitionHistory(Guid accountId, CancellationToken token)
    {
        var definitionIds = await context.QuestDefinition.AsNoTracking().Where(row => row.AccountId == accountId && row.SystemQuestId == null).Select(row => row.Id).ToArrayAsync(token);
        var terms = await QuestTermsDataService.Read(context, accountId, definitionIds, token);
        var archived = await context.QuestCommandHistory.AsNoTracking().Where(row => row.AccountId == accountId && row.ActionCode == "archive-definition" && row.QuestDefinitionRevisionId != null)
            .Select(row => row.QuestDefinitionRevisionId!.Value).ToListAsync(token);
        return new(terms, archived);
    }
}
