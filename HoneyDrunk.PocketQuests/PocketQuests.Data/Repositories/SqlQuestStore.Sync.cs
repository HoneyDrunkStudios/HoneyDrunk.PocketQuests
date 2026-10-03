using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.Entities;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Aggregates;
using System.Text.Json;

namespace PocketQuests.Data.Repositories;

/// <summary>Validates offline timestamps under the same SQL lock and transaction as the command receipt.</summary>
public sealed partial class SqlQuestStore
{
    /// <inheritdoc />
    public async Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token)
    {
        if (deviceId == Guid.Empty || bootId == Guid.Empty)
            throw new ArgumentException("Device and process identifiers are required.");
        var anchor = new SyncAnchorEntity { Id = Guid.NewGuid(), DeviceId = deviceId, BootId = bootId, DeviceUtc = deviceUtc, ServerUtc = now };
        await Transact(identity, null, null, now, token, issuingAnchor: anchor);
        return new(anchor.Id, deviceId, bootId, anchor.ServerUtc, deviceUtc, anchor.RecordedTimeFloor);
    }

    private async Task<DateTimeOffset> RecordedAt(Guid accountId, QuestCommand command, QuestAggregate aggregate, DateTimeOffset receivedAt, DateTimeOffset logicalNow, CancellationToken token)
    {
        if (command.RecordedTime is not { } proof)
        {
            if (logicalNow > receivedAt.AddSeconds(5))
                throw new ArgumentException("Server time is behind committed account history. Reconnect when its clock is reconciled.");
            return logicalNow;
        }

        var anchor = await db.SyncAnchors.SingleOrDefaultAsync(a => a.AccountId == accountId && a.Id == proof.AnchorId, token)
            ?? throw new ArgumentException("Offline time anchor belongs to another account or is unavailable. Keep this action for reconciliation.");
        if (proof.BootId != anchor.BootId || proof.Ordinal <= anchor.LastOrdinal || !double.IsFinite(proof.ElapsedMilliseconds)
            || proof.ElapsedMilliseconds < anchor.LastElapsedMilliseconds || proof.ElapsedMilliseconds < 0)
            throw new ArgumentException("Offline clock ordering cannot be verified. Preserve the action and reconnect; it will not be backdated automatically.");
        DateTimeOffset recorded;
        try
        {
            recorded = anchor.ServerUtc.AddMilliseconds(proof.ElapsedMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException("Offline elapsed time is outside the supported range.");
        }

        var wallElapsed = (proof.DeviceUtc - anchor.DeviceUtc).TotalMilliseconds;
        if (Math.Abs(wallElapsed - proof.ElapsedMilliseconds) > TimeSpan.FromMinutes(2).TotalMilliseconds || recorded > receivedAt.AddSeconds(5))
            throw new ArgumentException("Your device clock changed or moved ahead of server time. This action needs reconciliation; no final reward was granted.");

        // The issuance floor orders actions after history the device has already seen.
        // It is fixed on this anchor, never recomputed from later commands or added to elapsed time.
        // This preserves delayed offline actions and prevents refreshes from accumulating clock lead.
        var anchored = JsonSerializer.Deserialize<OccurrenceView[]>(anchor.Snapshot)!;

        // Older anchors already contain immutable visible event times. Recover their floor
        // from that snapshot only, so a previously rejected pending Undo can be retried unchanged.
        var floor = anchor.RecordedTimeFloor ?? anchored.SelectMany(o => new[] { o.Occurrence.AcceptedAt, o.Completion?.RecordedAt ?? DateTimeOffset.MinValue, o.Occurrence.Lifecycle?.FrozenAt ?? DateTimeOffset.MinValue, o.Occurrence.Lifecycle?.AbandonedAt ?? DateTimeOffset.MinValue }).Append(anchor.ServerUtc).Max();
        if (recorded < floor)
            recorded = floor;
        if (recorded > receivedAt.AddSeconds(5))
            throw new ArgumentException("The anchored account clock is ahead of server time. Preserve the action and reconnect after clock reconciliation.");
        if (command.Action == QuestActions.Complete)
        {
            var current = aggregate.Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new KeyNotFoundException();
            var snapshot = anchored.SingleOrDefault(o => o.Occurrence.Id == current.Id)?.Occurrence;
            if (snapshot is null && current.Lifecycle?.SourceAnchorId != anchor.Id)
                throw new ArgumentException("This occurrence was not available in the anchored session.");
            if (snapshot is not null)
            {
                // A later definition edit cannot reprice the action the device actually recorded.
                // Conflicting later explicit abandonment/freeze requires review instead of silent replacement.
                if (current.Lifecycle?.AbandonedAt is not null || current.Lifecycle?.FrozenAt is not null)
                    throw new ArgumentException("This commitment was paused or abandoned after the cached snapshot. Reconcile the recorded action explicitly.");
                var completed = aggregate.Completions.Any(c => c.OccurrenceId == current.Id && aggregate.Undos.All(u => u.CompletionId != c.Id));
                if (!completed)
                    aggregate.Occurrences[aggregate.Occurrences.IndexOf(current)] = snapshot with { ParentId = current.ParentId };
            }
        }

        anchor.LastOrdinal = proof.Ordinal;
        anchor.LastElapsedMilliseconds = proof.ElapsedMilliseconds;
        return recorded;
    }
}
