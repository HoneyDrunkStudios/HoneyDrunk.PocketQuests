using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Exports;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Quests.Definitions;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>The existing private export shape is reconstructed from explicit owned history without a stored account snapshot.</summary>
public sealed partial class RelationalQuestCommands
{
    /// <summary>Reads one consistent export without writing projections, initializing profiles or taking the command application lock.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing version-one private export contract.</returns>
    public async Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default)
    {
        await using var session = await Open(identity, token, readOnly: true);
        var account = await Account(session, identity, token);
        var aggregate = await Load(session, account, account.MutationVersion, token);
        var at = Max(now, account.LastRecordedAt);
        if (aggregate.Reconcile(at, RequestReconciliationLimit).HasMore)
            throw new ReconciliationPendingException("Recurring deliveries must finish reconciliation before a complete export can be produced.");
        var definitions = await session.Context.Set<QuestDefinitionEntity>().Where(d => d.AccountId == account.Id && d.SystemQuestId == null).Select(d => d.Id).ToArrayAsync(token);
        var revisions = await session.Context.Set<QuestDefinitionRevisionEntity>().Where(r => r.AccountId == account.Id && definitions.Contains(r.QuestDefinitionId))
            .OrderBy(r => r.QuestDefinitionId).ThenBy(r => r.Revision).ToListAsync(token);
        var archived = await session.Context.Set<QuestCommandHistoryEntity>().Where(r => r.AccountId == account.Id && r.ActionCode == "archive-definition").Select(r => r.QuestDefinitionRevisionId).ToListAsync(token);
        var terms = await ReadTerms(session, account.Id, token, revisions.Select(r => r.Id).ToArray());
        var result = new QuestExport(1, now.ToUniversalTime(), at, account.Id, aggregate.Project(at), [.. revisions.Select(r => new QuestDefinition(terms[r.Id], r.Revision, archived.Contains(r.Id)))], [.. aggregate.Completions], [.. aggregate.Undos]);
        await session.Transaction.CommitAsync(token);
        return result;
    }
}
