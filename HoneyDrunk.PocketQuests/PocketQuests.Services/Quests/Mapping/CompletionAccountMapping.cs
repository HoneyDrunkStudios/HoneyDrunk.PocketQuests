using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Schedules;
using QuestValues = PocketQuests.Domain.Services.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class CompletionAccountMapping
{
    internal static void ApplyTo(this QuestCommit change, QuestCompletionRows rows, SyncAnchorEntity? anchor)
    {
        var account = change.Account;
        account.SelectedBadgeId = change.State.Profile.BadgeId;
        account.SelectedFrameId = change.State.Profile.FrameId;
        account.MutationVersion = change.Version;
        account.ProjectionVersion = change.Version;
        account.ProjectionAsOfAt = change.HasPending && account.ProjectionAsOfAt < change.RecordedAt ? account.ProjectionAsOfAt : change.HasPending ? change.RecordedAt : change.ProjectionAt;
        account.HasPendingReconciliation = change.HasPending;
        account.LastRecordedAt = change.ProjectionAt;
        account.ModifiedAt = account.ModifiedAt > change.Now ? account.ModifiedAt : change.Now;
        var seriesById = rows.Series.ToDictionary(row => row.Id);
        foreach (var series in change.Aggregate.Schedule.Series)
        {
            var row = seriesById[series.Id];
            DateOnly? next = null;
            if (!series.Stopped)
            {
                try
                {
                    var date = Scheduling.Recurrence(Scheduling.ParseDate(series.Anchor), series.Cadence, series.Interval, series.NextSequence, series.PauseDays);
                    if (date.Year < 9999)
                        next = QuestValues.Date(Scheduling.DateText(date));
                }
                catch (ArgumentOutOfRangeException)
                {
                    // An exhausted calendar has no representable next delivery.
                }
            }

            if (row.NextSequence == series.NextSequence && row.NextDeliveryOn == next)
                continue;
            row.NextSequence = series.NextSequence;
            row.NextDeliveryOn = next;
            row.ModifiedAt = row.ModifiedAt > change.Now ? row.ModifiedAt : change.Now;
        }

        if (change.Command.RecordedTime is { } proof && anchor is not null)
        {
            anchor.LastOrdinal = proof.Ordinal;
            anchor.LastElapsedMilliseconds = proof.ElapsedMilliseconds;
            anchor.ModifiedAt = anchor.ModifiedAt > change.Now ? anchor.ModifiedAt : change.Now;
        }
    }
}
