using NodaTime;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Domain.Quests.Aggregates;

/// <summary>Explicit zone changes preserve final cutoffs and segment local-day accounting.</summary>
public sealed partial class QuestAggregate
{
    private string ZoneAt(DateTimeOffset at) => (Profile.ZoneHistory ?? []).Where(c => c.At <= at).OrderBy(c => c.At).LastOrDefault()?.To
        ?? Profile.ZoneHistory?.FirstOrDefault()?.From ?? Zone;

    private LocalDate ActivityDay(DateTimeOffset at)
    {
        var changes = Profile.ZoneHistory ?? [];
        var selected = changes.Count > 0 ? changes[0].From : Zone;
        var offset = 0;
        foreach (var change in changes.Where(c => c.At <= at).OrderBy(c => c.At))
        {
            offset += Period.Between(Scheduling.LocalDay(change.At, change.To), Scheduling.LocalDay(change.At, change.From), PeriodUnits.Days).Days;
            selected = change.To;
        }

        return Scheduling.LocalDay(at, selected).PlusDays(offset);
    }

    private void ChangeZone(QuestCommand command, DateTimeOffset now)
    {
        var next = Scheduling.Zone(command.NewZone ?? throw new ArgumentException("Choose a timezone.")).Id;
        if (command.ExpectedZone != Zone)
            throw new InvalidOperationException("The selected timezone changed. Reload the preview.");
        Progression.Require(command.ConfirmZoneChange, "Review the new deadlines and confirm. Active deadlines moving into the past become missed immediately; finalized history stays unchanged.");
        if (next == Zone)
            return;
        foreach (var occurrence in Occurrences.ToArray())
        {
            var life = occurrence.Lifecycle ?? new();

            // Persist the original zone even for legacy finalized rows.
            life = life with { DeadlineZone = life.DeadlineZone ?? Zone };
            var editable = Surviving(occurrence.Id) is null && life.AbandonedAt is null
                && (life.FrozenAt is not null || occurrence.Deadline is null || occurrence.Deadline > now);
            Occurrences[Occurrences.IndexOf(occurrence)] = occurrence with {
                Deadline = editable && occurrence.DueDate is not null ? Scheduling.Deadline(Scheduling.ParseDate(occurrence.DueDate), next) : occurrence.Deadline,
                Lifecycle = editable ? life with { DeadlineZone = next } : life };
        }

        Profile = Profile with { ZoneHistory = (Profile.ZoneHistory ?? []).Add(new(Zone, next, now)) };
        Zone = next;
        Reconcile(now);
    }
}
