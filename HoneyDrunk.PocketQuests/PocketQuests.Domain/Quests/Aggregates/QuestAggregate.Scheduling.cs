using NodaTime;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using System.Security.Cryptography;
using System.Text;

namespace PocketQuests.Domain.Quests.Aggregates;

/// <summary>Durable recurrence and effective pause transitions under the account transaction.</summary>
public sealed partial class QuestAggregate
{
    /// <summary>Gets the durable series and pause state.</summary>
    public ScheduleState Schedule { get; private set; } = schedule ?? ScheduleState.Empty;

    /// <summary>Materializes due deliveries exactly once before reads and commands.</summary>
    /// <param name="now">Authoritative reconciliation time.</param>
    public void Reconcile(DateTimeOffset now)
    {
        var progress = Reconcile(now, _actionDeliveryBudget ?? int.MaxValue);
        if (_actionDeliveryBudget is not null)
        {
            _actionDeliveryBudget -= progress.Processed;
            _actionDeliveries += progress.Processed;
            _actionHasMoreDeliveries |= progress.HasMore;
        }
    }

    /// <summary>Processes at most the requested number of due deliveries; repeated steps produce the original unbounded result.</summary>
    /// <param name="now">Authoritative reconciliation time.</param>
    /// <param name="maximumDeliveries">Nonnegative work budget for this step, without discarding older due deliveries; zero only inspects whether work is pending.</param>
    /// <returns>The processed cursor count and whether another step is needed.</returns>
    public ReconciliationProgress Reconcile(DateTimeOffset now, int maximumDeliveries)
    {
        if (maximumDeliveries < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDeliveries));
        var processed = 0;
        var today = Scheduling.LocalDay(now, Zone);
        var pending = new PriorityQueue<(QuestSeries series, LocalDate date, DateTimeOffset at), (DateTimeOffset at, Guid id)>();
        foreach (var series in Schedule.Series.Where(s => !s.Stopped))
            QueueNext(series);
        while (processed < maximumDeliveries && pending.TryDequeue(out var item, out _))
        {
            processed++;
            var (series, date, deliveredAt) = item;
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{series.Id:N}/{series.Version}/{series.NextSequence}")).AsSpan(0, 16));
            if (Occurrences.All(o => o.Id != id))
            {
                var due = Scheduling.DateText(date);
                var hasPenalty = series.Quest.PenaltyPercent > 0;
                var progress = Project(deliveredAt);
                var eligible = Progression.Eligible(series.Quest, progress.Categories.ToDictionary(c => c.Id, c => c.Xp), progress.Skills.ToDictionary(c => c.Id, c => c.Xp));
                var accepted = eligible && (!hasPenalty || series.AutoAcceptPenalty);
                var deadline = Scheduling.Deadline(date, Zone);
                var pausedAt = OpenPause(series.Quest.CategoryId);
                var lifecycle = new OccurrenceLifecycle(series.Id, series.NextSequence, series.Version, FrozenAt: accepted && pausedAt is { } pause && deadline > pause ? pause : null, LockedLoss: accepted && hasPenalty ? Loss(series.Quest) : null, LossCategoryId: accepted && hasPenalty ? series.Quest.CategoryId : null, Unaccepted: !accepted, DeadlineZone: Zone);
                Occurrences.Add(new(id, series.Quest, due, deadline, deliveredAt, series.PlannedTime, Lifecycle: lifecycle));
            }

            series = series with { NextSequence = series.NextSequence + 1 };
            ReplaceSeries(series);
            QueueNext(series);
        }

        return new(processed, pending.Count != 0);

        void QueueNext(QuestSeries series)
        {
            LocalDate date;
            try
            {
                date = Scheduling.Recurrence(Scheduling.ParseDate(series.Anchor), series.Cadence, series.Interval, series.NextSequence, series.PauseDays);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            if (date > today || date.Year >= 9999)
                return;
            var at = Scheduling.Zone(Zone).AtLeniently(date.AtMidnight()).ToDateTimeOffset();
            if (series.EffectiveAt > at)
                at = series.EffectiveAt.Value;
            if (OpenPause(series.Quest.CategoryId) is { } pausedAt && (date > Scheduling.LocalDay(pausedAt, Zone) || at > pausedAt))
                return;
            pending.Enqueue((series, date, at), (at: at, id: series.Id));
        }
    }

    private bool IsPaused(string categoryId) => Schedule.AccountPaused || Schedule.PausedCategories.Contains(categoryId);

    private DateTimeOffset? OpenPause(string categoryId) => IsPaused(categoryId)
        ? Schedule.Pauses.Single(p => p.CategoryId == categoryId && p.EndedAt is null).StartedAt : null;

    private int ActiveDaysBetween(string categoryId, LocalDate first, LocalDate last)
    {
        var days = Period.Between(first, last, PeriodUnits.Days).Days;
        foreach (var pause in Schedule.Pauses.Where(p => p.CategoryId == categoryId))
        {
            var start = ActivityDay(pause.StartedAt);
            var end = pause.EndedAt is { } at ? ActivityDay(at) : last;
            var from = start > first ? start : first;
            var to = end < last ? end : last;
            if (to > from)
                days -= Period.Between(from, to, PeriodUnits.Days).Days;
        }

        return Math.Max(0, days);
    }

    private void SaveSeries(QuestCommand command, DateTimeOffset now)
    {
        Progression.Require(command.SeriesId is { } id && id != Guid.Empty, "A series ID is required.");
        var quest = Catalog.Quests.SingleOrDefault(q => q.Id == command.QuestId)
            ?? Definitions.SingleOrDefault(d => d.Quest.Id == command.QuestId && !d.Archived)?.Quest
            ?? throw new ArgumentException("Choose an available quest.");
        RequireEligible(quest, now);
        Progression.Require(!IsPaused(quest.CategoryId), "Resume the category before starting or editing recurrence.");
        var date = Scheduling.ParseDate(command.DueDate ?? throw new ArgumentException("Choose a first delivery date."));
        Progression.Require(date >= Scheduling.LocalDay(now, Zone) && date.Year < 9999, "Delivery date must be today or later, before year 9999.");
        Progression.Require(command.Cadence is not null && Enum.IsDefined(command.Cadence.Value) && command.Interval is >= 1 and <= 999, "Cadence: choose a unit and interval from 1 to 999.");
        ValidatePlannedTime(command.DueDate, command.PlannedTime);
        if (command.ConfirmPenalty)
            _ = PenaltyTerms(quest, command, Scheduling.Deadline(date, Zone));
        var prior = Schedule.Series.SingleOrDefault(s => s.Id == command.SeriesId);
        if (command.ExpectedRevision != (prior?.Version ?? 0))
            throw new InvalidOperationException("Series changed; reopen before editing.");
        Progression.Require(prior is null || !prior.Stopped, "Stopped series stay stopped; explicitly create a new series.");
        var series = new QuestSeries(command.SeriesId!.Value, quest, command.DueDate!, command.Cadence!.Value, command.Interval!.Value, (prior?.Version ?? 0) + 1, PlannedTime: command.PlannedTime, AutoAcceptPenalty: command.ConfirmPenalty, EffectiveAt: now);
        if (prior is null)
            Schedule = Schedule with { Series = Schedule.Series.Add(series) };
        else
            ReplaceSeries(series);
        Reconcile(now);
    }

    private void ReplaceSeries(QuestSeries series) => Schedule = Schedule with { Series = [.. Schedule.Series.Select(s => s.Id == series.Id ? series : s)] };

    private void StopSeries(QuestCommand command, DateTimeOffset now)
    {
        var series = Schedule.Series.SingleOrDefault(s => s.Id == command.SeriesId) ?? throw new KeyNotFoundException();
        if (series.Stopped)
            return;
        ReplaceSeries(series with { Stopped = true });
        foreach (var occurrence in Occurrences.Where(o => o.Lifecycle?.SeriesId == series.Id && Surviving(o.Id) is null && o.Lifecycle?.AbandonedAt is null && o.Lifecycle?.Unaccepted != true
            && (o.Lifecycle?.FrozenAt is not null || o.Deadline is null || now < o.Deadline)).ToArray())
            FreezeOccurrence(occurrence, now, true);
    }

    private void FreezeOccurrence(Occurrence occurrence, DateTimeOffset now, bool individual)
    {
        var life = occurrence.Lifecycle ?? new();
        Occurrences[Occurrences.IndexOf(occurrence)] = occurrence with { Lifecycle = life with { FrozenAt = life.FrozenAt ?? now, IndividuallyFrozen = life.IndividuallyFrozen || individual } };
    }

    private void Pause(QuestCommand command, DateTimeOffset now, bool paused)
    {
        Progression.Require(command.CategoryId is null || Catalog.Categories.Any(c => c.Id == command.CategoryId), "Choose a core category or all categories.");
        if (!paused)
        {
            // Drain only the retained pre-pause segment before moving future cursor dates.
            // Relational commands perform and commit bounded continuation before this action.
            Reconcile(now);
            if (_actionHasMoreDeliveries)
                throw new InvalidOperationException("Retained pre-pause deliveries must finish reconciliation before resume.");
        }

        var previously = Catalog.Categories.ToDictionary(c => c.Id, c => IsPaused(c.Id));
        Schedule = command.CategoryId is null
            ? Schedule with { AccountPaused = paused }
            : Schedule with { PausedCategories = paused ? [.. Schedule.PausedCategories.Append(command.CategoryId).Distinct(StringComparer.Ordinal)] : [.. Schedule.PausedCategories.Where(id => id != command.CategoryId)] };
        foreach (var category in Catalog.Categories)
        {
            var before = previously[category.Id];
            var after = IsPaused(category.Id);
            if (before == after)
                continue;
            if (after)
            {
                Schedule = Schedule with { Pauses = Schedule.Pauses.Add(new(category.Id, now)) };
                foreach (var occurrence in Occurrences.Where(o => o.Quest.CategoryId == category.Id && Surviving(o.Id) is null && o.Lifecycle?.AbandonedAt is null && o.Lifecycle?.Unaccepted != true
                    && (o.Lifecycle?.FrozenAt is not null || o.Deadline is null || now < o.Deadline)).ToArray())
                    FreezeOccurrence(occurrence, now, false);
            }
            else
            {
                var window = Schedule.Pauses.Single(p => p.CategoryId == category.Id && p.EndedAt is null);
                var shift = Period.Between(ActivityDay(window.StartedAt), ActivityDay(now), PeriodUnits.Days).Days;
                Schedule = Schedule with { Pauses = [.. Schedule.Pauses.Select(p => p == window ? p with { EndedAt = now } : p)],
                    Series = [.. Schedule.Series.Select(s => s.Quest.CategoryId == category.Id && !s.Stopped ? s with { PauseDays = s.PauseDays + shift } : s)] };
                foreach (var occurrence in Occurrences.Where(o => o.Quest.CategoryId == category.Id && o.Lifecycle?.FrozenAt is not null && !o.Lifecycle.IndividuallyFrozen).ToArray())
                    Thaw(occurrence, now);
            }
        }

        Reconcile(now);
    }

    private void Thaw(Occurrence occurrence, DateTimeOffset now)
    {
        var life = occurrence.Lifecycle!;
        var days = Period.Between(ActivityDay(life.FrozenAt!.Value), ActivityDay(now), PeriodUnits.Days).Days;
        var date = occurrence.DueDate is null ? (LocalDate?)null : Scheduling.ParseDate(occurrence.DueDate).PlusDays(days);
        Occurrences[Occurrences.IndexOf(occurrence)] = occurrence with { DueDate = date is null ? null : Scheduling.DateText(date.Value),
            Deadline = date is null ? null : Scheduling.Deadline(date.Value, Zone), Lifecycle = life with { FrozenAt = null, IndividuallyFrozen = false, DeadlineZone = Zone } };
    }

    private void ResumeOccurrence(QuestCommand command, DateTimeOffset now)
    {
        var occurrence = Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new KeyNotFoundException();
        Progression.Require(!IsPaused(occurrence.Quest.CategoryId), "Resume the category before this commitment.");
        if (occurrence.Lifecycle?.FrozenAt is not null)
            Thaw(occurrence, now);
    }

    private void Abandon(QuestCommand command, DateTimeOffset now)
    {
        var occurrence = Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new KeyNotFoundException();
        Progression.Require(Surviving(occurrence.Id) is null, "Undo a completion before abandoning it.");
        if (occurrence.Lifecycle?.AbandonedAt is not null)
            return;
        Progression.Require(occurrence.Lifecycle?.Unaccepted != true, "An unaccepted offer is not a commitment.");
        Progression.Require(occurrence.Lifecycle?.LockedLoss is null || (command.ConfirmPenalty && command.AcceptedLoss == occurrence.Lifecycle.LockedLoss), "Confirm the exact locked loss before abandonment.");
        var life = occurrence.Lifecycle ?? new();
        Occurrences[Occurrences.IndexOf(occurrence)] = occurrence with { Lifecycle = life with { AbandonedAt = now, FrozenAt = null } };
    }
}
