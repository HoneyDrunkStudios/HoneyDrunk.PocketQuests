using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Queries.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Reads owned quest state, replay and retained terms within the shared transaction.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class QuestReadDataService(AppDbContext context) : IQuestReadDataService
{
    /// <inheritdoc />
    public Task<QuestStateRows> ReadCurrentState(Guid accountId, CancellationToken token) => QuestStateQueryDataService.Read(context, accountId, token);

    /// <inheritdoc />
    public Task<QuestReplayRows> ReadReplay(Guid accountId, long version, CancellationToken token) => QuestReplayQueryDataService.Read(context, accountId, version, token);

    /// <inheritdoc />
    public Task<QuestProjectionRows> ReadProjections(Guid accountId, CancellationToken token) => QuestProjectionQueryDataService.Read(context, accountId, token);

    /// <inheritdoc />
    public Task<QuestTermsRows> ReadTerms(Guid accountId, Guid[] definitionIds, CancellationToken token) => QuestTermsQueryDataService.Read(context, accountId, definitionIds, token);

    /// <inheritdoc />
    public Task<QuestTermsRows> ReadSelectedTerms(Guid accountId, Guid[] revisionIds, CancellationToken token) => QuestTermsQueryDataService.ReadSelected(context, accountId, revisionIds, token);

    /// <inheritdoc />
    public async Task<QuestDefinitionHistoryRows> ReadDefinitionHistory(Guid accountId, CancellationToken token)
    {
        var definitionIds = await context.QuestDefinition.AsNoTracking().Where(row => row.AccountId == accountId && row.SystemQuestId == null).Select(row => row.Id).ToArrayAsync(token);
        var terms = await QuestTermsQueryDataService.Read(context, accountId, definitionIds, token);
        var archived = await context.QuestCommandHistory.AsNoTracking().Where(row => row.AccountId == accountId && row.ActionCode == "archive-definition" && row.QuestDefinitionRevisionId != null)
            .Select(row => row.QuestDefinitionRevisionId!.Value).ToListAsync(token);
        return new(terms, archived);
    }
}
