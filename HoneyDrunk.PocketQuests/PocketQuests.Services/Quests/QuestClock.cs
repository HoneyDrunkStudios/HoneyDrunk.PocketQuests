using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Services.Quests;

internal static class QuestClock
{
    internal static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => (first > second ? first : second).ToUniversalTime();

    internal static DateTimeOffset Resolve(AccountEntity account, QuestCommand command, QuestAggregate aggregate, QuestCompletionRows rows, IReadOnlyDictionary<Guid, Quest> terms, SyncAnchorEntity? anchor, DateTimeOffset receivedAt, DateTimeOffset logicalNow)
    {
        if (command.RecordedTime is not { } proof)
        {
            if (logicalNow > receivedAt.AddSeconds(5))
                throw new SyncClockNotReadyException("Server time is behind committed account history.");
            return logicalNow;
        }

        if (anchor is null)
            throw new QuestValidationException("Offline time anchor belongs to another account or is unavailable.");
        if (anchor.AccountId != account.Id || anchor.InvalidatedAt is not null || proof.BootId != anchor.BootId || proof.Ordinal <= anchor.LastOrdinal
            || !double.IsFinite(proof.ElapsedMilliseconds) || proof.ElapsedMilliseconds < anchor.LastElapsedMilliseconds || proof.ElapsedMilliseconds < 0)
            throw new QuestValidationException("Offline clock ordering cannot be verified. Preserve the action for reconciliation.");
        DateTimeOffset recorded;
        try
        {
            recorded = anchor.ServerAt.AddMilliseconds(proof.ElapsedMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new QuestValidationException("Offline elapsed time is outside the supported range.");
        }

        if (Math.Abs((proof.DeviceUtc - anchor.DeviceAt).TotalMilliseconds - proof.ElapsedMilliseconds) > TimeSpan.FromMinutes(2).TotalMilliseconds)
            throw new QuestValidationException("The device wall clock changed relative to elapsed time.");

        // Same retry/permanent distinction as reviewed SYNC02 head 7de99e6. No wall-clock expiry.
        if (recorded > receivedAt.AddSeconds(5))
            throw new SyncClockNotReadyException("The recorded action is ahead of server time; retry the same payload.");
        recorded = Max(recorded, anchor.RecordedTimeFloorAt);
        if (recorded > receivedAt.AddSeconds(5))
            throw new SyncClockNotReadyException("The fixed anchor floor is ahead of server time.");
        if (command.Action == QuestActions.Complete)
        {
            var occurrence = aggregate.Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new QuestNotFoundException("Occurrence was not found.");
            var anchored = QuestReplay.Through(rows, account, terms, anchor.IssuedMutationVersion).Aggregate;
            anchored.Reconcile(Max(anchor.ServerAt, anchor.RecordedTimeFloorAt));
            var snapshot = anchored.Occurrences.SingleOrDefault(o => o.Id == occurrence.Id);
            if (snapshot is null && occurrence.Lifecycle?.SourceAnchorId != anchor.Id)
                throw new QuestValidationException("Occurrence was not available in the anchored session.");
            if (occurrence.Lifecycle?.FrozenAt is not null || occurrence.Lifecycle?.AbandonedAt is not null)
                throw new QuestValidationException("Occurrence was paused or abandoned; reconcile the recorded action.");
            if (snapshot is not null && !aggregate.Completions.Any(c => c.OccurrenceId == occurrence.Id && aggregate.Undos.All(u => u.CompletionId != c.Id)))
                aggregate.Occurrences[aggregate.Occurrences.IndexOf(occurrence)] = snapshot with { ParentId = occurrence.ParentId };
        }

        return recorded;
    }
}
