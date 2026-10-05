using PocketQuests.Contracts.Models.Reconciliation;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Schedules;
using PocketQuests.Services.Quests;
using System.Globalization;
using DomainProgress = PocketQuests.Domain.Models.Schedules.ReconciliationProgress;

namespace PocketQuests.Services.Reconciliation;

/// <summary>Advances bounded retained work without rewriting idle accounts.</summary>
/// <param name="data">Account selection, due-work checks and transaction ownership.</param>
/// <param name="quests">Shared source and mutation preparation.</param>
public sealed class ReconciliationService(IAccountDataService data, QuestService quests) : IReconciliationService
{
    /// <inheritdoc />
    public async Task<ReconciliationBatch> ReconcileAccounts(DateTimeOffset now, ReconciliationCursor? after = null, int maximumAccounts = 10, int maximumDeliveries = 100, CancellationToken token = default)
    {
        if (maximumAccounts is < 1 or > 50 || maximumDeliveries is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumAccounts));
        now = now.ToUniversalTime();
        var rows = await data.GetReconciliationCandidates(now, after?.ProjectionAsOfAt, after?.AccountId, maximumAccounts, IdentityProtocol.Active, token);
        var next = rows.Count > maximumAccounts ? new ReconciliationCursor(rows[maximumAccounts - 1].ProjectionAsOfAt, rows[maximumAccounts - 1].Id) : null;
        var reconciled = 0;
        var deliveries = 0;
        foreach (var account in rows.Take(maximumAccounts))
        {
            var at = QuestClock.Max(now, account.LastRecordedAt);
            var day = Scheduling.LocalDay(at, account.TimeZoneId);
            var date = DateOnly.ParseExact(Scheduling.DateText(day), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var needed = account.HasPendingReconciliation || day != Scheduling.LocalDay(account.ProjectionAsOfAt, account.TimeZoneId)
                || await data.HasDueWork(account.Id, date, at, !account.IsAccountPaused, token);
            if (!needed)
                continue;
            try
            {
                var progress = await Reconcile(new("honeydrunk-identity", account.IdentityUserId), now, maximumDeliveries, token);
                reconciled++;
                deliveries += progress.Processed;
            }
            catch (UnauthorizedAccessException)
            {
                // A concurrent private lifecycle transition now owns this account's final state.
            }
        }

        return new(Math.Min(rows.Count, maximumAccounts), reconciled, deliveries, next);
    }

    internal Task<DomainProgress> Reconcile(AccountIdentity identity, DateTimeOffset now, int maximumDeliveries = 100, CancellationToken token = default)
    {
        if (maximumDeliveries is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumDeliveries));
        return data.ExecuteInTransaction(Perform, token);

        async Task<DomainProgress> Perform(CancellationToken cancellationToken)
        {
            await quests.RequireAccess(identity, true, cancellationToken);
            var account = await quests.Account(identity, cancellationToken);
            var rows = await data.ReadCurrentState(account.Id, cancellationToken);
            var aggregate = QuestService.Current(account, rows);
            var at = QuestClock.Max(now, account.LastRecordedAt);
            var progress = aggregate.Reconcile(at, maximumDeliveries);
            var projectionDue = Scheduling.LocalDay(at, account.TimeZoneId) != Scheduling.LocalDay(account.ProjectionAsOfAt, account.TimeZoneId)
                || await data.HasDueWork(account.Id, DateOnly.FromDateTime(at.UtcDateTime), at, false, cancellationToken);
            if (progress.Processed > 0 || account.HasPendingReconciliation || projectionDue)
                await quests.StageReconciliation(identity, account, rows, aggregate, at, now.ToUniversalTime(), maximumDeliveries, progress.HasMore, cancellationToken);
            return progress;
        }
    }
}
