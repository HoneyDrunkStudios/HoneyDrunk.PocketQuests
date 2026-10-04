using Microsoft.EntityFrameworkCore;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Schedules;
using System.Data;
using System.Globalization;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Read-only and bounded maintenance workflows.</summary>
public sealed partial class QuestService
{
    /// <summary>Reads one consistent export without account writes or an exclusive command lock.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing version-one private export contract.</returns>
    public async Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default)
    {
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await RequireAccess(identity, false, token);
        var account = await Account(identity, token);
        var aggregate = await accountService.LoadCurrentAsync(account, token);
        var at = Max(now, account.LastRecordedAt);
        if (aggregate.Reconcile(at, RequestReconciliationLimit).HasMore)
            throw new ReconciliationPendingException("Recurring deliveries must finish reconciliation before a complete export can be produced.");
        var definitions = await definitionService.ReadHistoryAsync(account.Id, token);
        var result = new QuestExport(1, now.ToUniversalTime(), at, account.Id, aggregate.Project(at), [.. definitions], [.. aggregate.Completions], [.. aggregate.Undos]);
        await transaction.CommitAsync(token);
        return result;
    }

    /// <summary>Reads a bounded occurrence page without writing state or taking the command lock.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="after">Exclusive creation cursor.</param>
    /// <param name="size">Page size from one through one hundred.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current bounded page.</returns>
    public async Task<QuestOccurrencePage> ReadOccurrences(AccountIdentity identity, int after, int size, DateTimeOffset now, CancellationToken token = default)
    {
        if (after < 0 || size is < 1 or > 100)
            throw new QuestValidationException("Use a nonnegative cursor and a page size between one and one hundred.");
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await RequireAccess(identity, false, token);
        var account = await Account(identity, token);
        var page = await occurrenceService.ReadPageAsync(account, after, size, Max(now, account.LastRecordedAt), token);
        await transaction.CommitAsync(token);
        return page;
    }

    /// <summary>Advances retained backlogs and timed projections without account creation or repeated same-day idle writes.</summary>
    /// <param name="now">Maintenance host clock.</param>
    /// <param name="after">Cursor from the previous bounded page, or null to begin another round.</param>
    /// <param name="maximumAccounts">At most fifty accounts per scan.</param>
    /// <param name="maximumDeliveries">At most one thousand recurrence steps per account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Observed work and the next private scan cursor.</returns>
    public async Task<ReconciliationBatch> ReconcileAccounts(DateTimeOffset now, ReconciliationCursor? after = null, int maximumAccounts = 10, int maximumDeliveries = RequestReconciliationLimit, CancellationToken token = default)
    {
        if (maximumAccounts is < 1 or > 50 || maximumDeliveries is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumAccounts));
        now = now.ToUniversalTime();
        BeginOperation();
        var rows = await accountData.GetReconciliationCandidatesAsync(now, after?.ProjectionAsOfAt, after?.AccountId, maximumAccounts, IdentityProtocol.Active, token);
        var next = rows.Count > maximumAccounts ? new ReconciliationCursor(rows[maximumAccounts - 1].ProjectionAsOfAt, rows[maximumAccounts - 1].Id) : null;
        var reconciled = 0;
        var deliveries = 0;
        foreach (var account in rows.Take(maximumAccounts))
        {
            var at = Max(now, account.LastRecordedAt);
            var day = Scheduling.LocalDay(at, account.TimeZoneId);
            var date = DateOnly.ParseExact(Scheduling.DateText(day), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var needed = account.HasPendingReconciliation || day != Scheduling.LocalDay(account.ProjectionAsOfAt, account.TimeZoneId)
                || await accountData.HasDueWorkAsync(account.Id, date, at, !account.IsAccountPaused, token);
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
                // A lifecycle transition may fence/erase this candidate after selection.
                // Its private lifecycle writer owns the final projection and no product work follows it.
            }
        }

        return new(Math.Min(rows.Count, maximumAccounts), reconciled, deliveries, next);
    }
}
