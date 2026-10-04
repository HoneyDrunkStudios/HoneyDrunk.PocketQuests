using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Schedules;
using System.Globalization;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Private maintenance scans a bounded keyset page and commits a bounded delivery step for each eligible account.</summary>
public sealed partial class RelationalQuestCommands
{
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
        await using var db = new RelationalQuestReadContext(new DbContextOptionsBuilder<RelationalQuestReadContext>().UseSqlServer(connectionString).Options);
        var query = db.Set<AccountEntity>().Where(a => a.ProjectionAsOfAt < now && a.MutationVersion > 0
            && !db.Set<ErasureMarkerEntity>().Any(m => m.Id == a.IdentityUserId)
            && !db.Set<AccountLifecycleStateEntity>().Any(b => b.IdentityUserId == a.IdentityUserId && b.StateCode != IdentityProtocol.Active));
        if (after is not null)
            query = query.Where(a => a.ProjectionAsOfAt > after.ProjectionAsOfAt || (a.ProjectionAsOfAt == after.ProjectionAsOfAt && a.Id.CompareTo(after.AccountId) > 0));
        var rows = await query.OrderBy(a => a.ProjectionAsOfAt).ThenBy(a => a.Id).Take(maximumAccounts + 1).ToListAsync(token);
        var next = rows.Count > maximumAccounts ? new ReconciliationCursor(rows[maximumAccounts - 1].ProjectionAsOfAt, rows[maximumAccounts - 1].Id) : null;
        var reconciled = 0;
        var deliveries = 0;
        foreach (var account in rows.Take(maximumAccounts))
        {
            var at = Max(now, account.LastRecordedAt);
            var day = Scheduling.LocalDay(at, account.TimeZoneId);
            var date = DateOnly.ParseExact(Scheduling.DateText(day), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var needed = account.HasPendingReconciliation || day != Scheduling.LocalDay(account.ProjectionAsOfAt, account.TimeZoneId)
                || (!account.IsAccountPaused && await db.Set<QuestSeriesEntity>().AnyAsync(s => s.AccountId == account.Id && s.StoppedAt == null && s.NextDeliveryOn <= date, token))
                || await db.Set<QuestOccurrenceEntity>().AnyAsync(o => o.AccountId == account.Id && o.StateCode == "Active" && o.DeadlineAt <= at, token);
            if (!needed)
                continue;
            try
            {
                var progress = await Reconcile(new("honeydrunk-identity", account.IdentityUserId), now, maximumDeliveries, token);
                reconciled++;
                deliveries += progress.Processed;
            }
            catch (SqlException error) when (error.Number == 51103)
            {
                // A lifecycle transition may fence/erase this candidate after selection.
                // Its private lifecycle writer owns the final projection and no product work follows it.
            }
        }

        return new(Math.Min(rows.Count, maximumAccounts), reconciled, deliveries, next);
    }
}
