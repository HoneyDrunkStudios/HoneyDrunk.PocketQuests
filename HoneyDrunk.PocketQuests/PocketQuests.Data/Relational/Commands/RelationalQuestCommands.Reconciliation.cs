using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Explicit bounded maintenance preserves every overdue delivery and records exact replay budgets.</summary>
public sealed partial class RelationalQuestCommands
{
    private const int RequestReconciliationLimit = 100;

    /// <summary>Persists one bounded delivery/projection step without creating any client receipt or consuming a client proof.</summary>
    /// <param name="identity">Verified canonical account selected by trusted maintenance.</param>
    /// <param name="now">Maintenance clock.</param>
    /// <param name="maximumDeliveries">One through one thousand due cursor steps.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The processed count and whether more due delivery work remains.</returns>
    public async Task<ReconciliationProgress> Reconcile(AccountIdentity identity, DateTimeOffset now, int maximumDeliveries = RequestReconciliationLimit, CancellationToken token = default)
    {
        if (maximumDeliveries is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumDeliveries));
        await using var session = await Open(identity, token);
        var account = await Account(session, identity, token);
        var aggregate = await Load(session, account, account.MutationVersion, token);
        var at = Max(now, account.LastRecordedAt);
        var progress = aggregate.Reconcile(at, maximumDeliveries);
        var projectionDue = Scheduling.LocalDay(at, account.TimeZoneId) != Scheduling.LocalDay(account.ProjectionAsOfAt, account.TimeZoneId)
            || await session.Context.Set<QuestOccurrenceEntity>().AnyAsync(o => o.AccountId == account.Id && o.StateCode == "Active" && o.DeadlineAt <= at, token);
        if (progress.Processed > 0 || account.HasPendingReconciliation || projectionDue)
            await PersistReconciliation(session, identity, account, aggregate, at, now.ToUniversalTime(), maximumDeliveries, progress.HasMore, token);
        await session.Transaction.CommitAsync(token);
        return progress;
    }

    private static Task PersistReconciliation(Session session, AccountIdentity identity, AccountEntity account, QuestAggregate aggregate, DateTimeOffset at, DateTimeOffset now, int limit, bool hasMore, CancellationToken token)
    {
        var command = new QuestCommand(Guid.NewGuid(), "$reconcile");
        return CommitMutation(session, identity, account, command, CommandDigest.Compute(command), aggregate, aggregate.Project(at), at, at, at, now, token, limit, internalTransition: true, hasPending: hasMore);
    }
}
