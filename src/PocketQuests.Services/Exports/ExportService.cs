using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Exports.Mapping;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Quests.Mapping;
using System.Data;
using ExportResponse = PocketQuests.Contracts.Responses.Exports.QuestExport;

namespace PocketQuests.Services.Exports;

/// <summary>Reads retained export sources under a repeatable-read transaction without account writebacks.</summary>
/// <param name="data">Account transaction and selected source queries.</param>
/// <param name="quests">Verified account access and current projection.</param>
/// <param name="currentAccount">Authenticated identity.</param>
/// <param name="clock">Host time.</param>
public sealed class ExportService(IAccountDataService data, QuestService quests, ICurrentAccount currentAccount, TimeProvider clock) : IExportService
{
    /// <inheritdoc />
    public async Task<ExportResponse> Read(CancellationToken token = default) =>
        (await Read(currentAccount.Identity.ToModel(), clock.GetUtcNow(), token)).ToModel();

    internal Task<QuestExport> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default)
    {
        return data.ExecuteInTransaction(Snapshot, token, IsolationLevel.RepeatableRead);

        async Task<QuestExport> Snapshot(CancellationToken cancellationToken)
        {
            await quests.RequireAccess(identity, false, cancellationToken);
            var account = await quests.Account(identity, cancellationToken);
            var rows = await data.ReadCurrentState(account.Id, cancellationToken);
            var aggregate = QuestService.Current(account, rows);
            var at = QuestClock.Max(now, account.LastRecordedAt);
            if (aggregate.Reconcile(at, QuestService.ReconciliationLimit).HasMore)
                throw new ReconciliationPendingException("Recurring deliveries must finish reconciliation before a complete export can be produced.");
            var history = await data.ReadDefinitionHistory(account.Id, cancellationToken);
            var terms = QuestTerms.Read(history.Terms);
            var definitions = history.Terms.DefinitionRevisions.OrderBy(row => row.QuestDefinitionId).ThenBy(row => row.Revision)
                .Select(row => row.ToModel(terms[row.Id], history.ArchivedRevisionIds.Contains(row.Id)));
            return aggregate.Project(at).ToExport(now.ToUniversalTime(), at, account.Id, definitions, aggregate.Completions, aggregate.Undos);
        }
    }
}
