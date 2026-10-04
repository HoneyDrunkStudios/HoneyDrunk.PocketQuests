using NodaTime;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Quests.Aggregates;

// Storage loads the immutable accepted snapshots and append-only completion/Undo
// events inside an account transaction; projections are rebuilt from surviving events.

/// <summary>Applies commands and rebuilds progression from immutable accepted terms and surviving events.</summary>
public sealed partial class QuestAggregate(string zone, IEnumerable<Occurrence>? occurrences = null,
    IEnumerable<Completion>? completions = null, IEnumerable<UndoEvent>? undos = null,
    IEnumerable<QuestDefinition>? definitions = null, PlayerProfile? profile = null, ScheduleState? schedule = null)
{
    private int? _actionDeliveryBudget;
    private int _actionDeliveries;
    private bool _actionHasMoreDeliveries;

    /// <summary>Gets the account's validated IANA timezone.</summary>
    public string Zone { get; private set; } = Scheduling.Zone(zone).Id;

    /// <summary>Gets accepted occurrence snapshots within the current transaction.</summary>
    public List<Occurrence> Occurrences { get; } = occurrences?.ToList() ?? [];

    /// <summary>Gets append-only completion events.</summary>
    public List<Completion> Completions { get; } = completions?.ToList() ?? [];

    /// <summary>Gets append-only completion reversals.</summary>
    public List<UndoEvent> Undos { get; } = undos?.ToList() ?? [];

    /// <summary>Validates and applies one command using authoritative server time.</summary>
    /// <param name="command">The command to validate and apply.</param>
    /// <param name="now">Authoritative server UTC time.</param>
    public void Apply(QuestCommand command, DateTimeOffset now) => _ = Apply(command, now, int.MaxValue);

    /// <summary>Applies the same command with a bounded budget for recurrence triggered by the action itself.</summary>
    /// <param name="command">The existing command contract.</param>
    /// <param name="now">Verified effective command time.</param>
    /// <param name="maximumDeliveries">Nonnegative delivery budget; zero defers all newly due deliveries.</param>
    /// <returns>Action-triggered cursor work and whether due deliveries remain.</returns>
    public ReconciliationProgress Apply(QuestCommand command, DateTimeOffset now, int maximumDeliveries)
    {
        if (maximumDeliveries < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDeliveries));
        _actionDeliveryBudget = maximumDeliveries;
        _actionDeliveries = 0;
        _actionHasMoreDeliveries = false;
        try
        {
            ApplyCore(command, now);
            return new(_actionDeliveries, _actionHasMoreDeliveries);
        }
        finally
        {
            _actionDeliveryBudget = null;
        }
    }

    /// <summary>Rebuilds all balances, streaks and entitlements after completion reversals.</summary>
    /// <param name="now">Authoritative server UTC time.</param>
    /// <returns>The authoritative state at the supplied time.</returns>
    public QuestState Project(DateTimeOffset now)
    {
        var today = Scheduling.LocalDay(now, Zone);
        var activityToday = ActivityDay(now);
        var surviving = Completions.Where(c => c.RecordedAt <= now && Undos.All(u => u.CompletionId != c.Id || u.RecordedAt > now)).OrderBy(c => c.RecordedAt).ThenBy(c => c.Id).ToArray();
        var categoryXp = Catalog.Categories.ToDictionary(x => x.Id, _ => 0L);
        var attributeXp = Catalog.Attributes.ToDictionary(x => x.Id, _ => 0L);
        var skillItems = SkillItems();
        var skillXp = skillItems.ToDictionary(x => x.Id, x => Progression.Seed(AssessmentAt(x.Id, now)));
        var streaks = new Dictionary<string, (LocalDate day, int count)>();
        var creditedDays = new Dictionary<(string category, LocalDate day), int>();
        var ledger = new List<XpEntry>();
        long overall = 0;
        var penalties = new List<PenaltyAssessment>();
        var events = surviving.Select(c => (at: c.RecordedAt, id: c.Id, completion: (Completion?)c, loss: (Occurrence?)null))
            .Concat(Occurrences.Select(o => (occurrence: o, at: AssessmentTime(o, now)))
                .Where(x => x.at is not null).Select(x => (at: x.at!.Value, id: x.occurrence.Id, completion: (Completion?)null, loss: (Occurrence?)x.occurrence)))
            .OrderBy(e => e.at).ThenBy(e => e.id);
        foreach (var entry in events)
        {
            if (entry.loss is { } loss)
            {
                var category = loss.Lifecycle!.LossCategoryId!;
                var locked = loss.Lifecycle.LockedLoss!.Value;
                var actual = Math.Min(categoryXp[category], locked);
                categoryXp[category] -= actual;
                penalties.Add(new(loss.Id, category, locked, actual, entry.at));
                ledger.Add(new(loss.Id, loss.Id, entry.at, "Category", category, -actual));
                continue;
            }

            var completion = entry.completion!;
            var quest = CompletionQuest(completion);
            var day = ActivityDay(completion.RecordedAt);
            var localDay = Scheduling.LocalDay(completion.RecordedAt, ZoneAt(completion.RecordedAt));
            var hasPrior = streaks.TryGetValue(quest.CategoryId, out var prior);
            var repeat = creditedDays.TryGetValue((category: quest.CategoryId, day: localDay), out var credited);
            var gap = hasPrior ? ActiveDaysBetween(quest.CategoryId, prior.day, day) : -1;
            var count = repeat ? credited : gap == 0 ? prior.count : gap == 1 ? prior.count + 1 : 1;
            creditedDays[(category: quest.CategoryId, day: localDay)] = count;
            streaks[quest.CategoryId] = (day, repeat && hasPrior ? prior.count : count);
            ledger.Add(new(completion.Id, completion.OccurrenceId, completion.RecordedAt, "Overall", "overall", quest.BaseXp));
            ledger.Add(new(completion.Id, completion.OccurrenceId, completion.RecordedAt, "Category", quest.CategoryId, quest.BaseXp + Progression.StreakBonus(quest.BaseXp, count)));
            overall += quest.BaseXp;
            categoryXp[quest.CategoryId] += quest.BaseXp + Progression.StreakBonus(quest.BaseXp, count);

            // Stable target order makes history-derived responses reproducible across processes.
            // ImmutableDictionary enumeration uses process-specific string hashing; reward values stay unchanged.
            foreach (var award in Progression.Allocate(quest.BaseXp, quest.Attributes).OrderBy(a => a.Key, StringComparer.Ordinal))
            {
                attributeXp[award.Key] += award.Value;
                ledger.Add(new(completion.Id, completion.OccurrenceId, completion.RecordedAt, "Attribute", award.Key, award.Value));
            }

            foreach (var award in Progression.Allocate(quest.BaseXp, quest.Skills).OrderBy(a => a.Key, StringComparer.Ordinal))
            {
                skillXp[award.Key] += award.Value;
                ledger.Add(new(completion.Id, completion.OccurrenceId, completion.RecordedAt, "Skill", award.Key, award.Value));
            }
        }

        var rank = Progression.GlobalRank(categoryXp);
        var views = Occurrences.Select(occurrence =>
        {
            var completion = Surviving(occurrence.Id, now);
            var status = occurrence.Lifecycle?.Unaccepted == true ? QuestStatus.Offered
                : completion is not null ? QuestStatus.Completed
                : occurrence.Lifecycle?.AbandonedAt is not null ? QuestStatus.Abandoned
                : occurrence.Lifecycle?.FrozenAt is not null ? QuestStatus.Frozen
                : occurrence.Deadline is { } deadline && now >= deadline ? QuestStatus.Missed : QuestStatus.Active;
            var canUndo = completion is not null && now >= completion.RecordedAt && now < completion.RecordedAt.Add(QuestRules.UndoWindow);
            return new OccurrenceView(occurrence, status, completion, canUndo, occurrence.DueDate is not null && occurrence.PlannedTime is not null ? Scheduling.Planned(occurrence.DueDate, occurrence.PlannedTime, occurrence.Lifecycle?.DeadlineZone ?? Zone) : null);
        }).ToImmutableArray();
        var currentStreaks = Catalog.Categories.Select(category =>
        {
            var has = streaks.TryGetValue(category.Id, out var last);
            var creditedToday = creditedDays.TryGetValue((category: category.Id, day: today), out var rateToday);
            var days = creditedToday ? rateToday : has && ActiveDaysBetween(category.Id, last.day, activityToday) <= 1 ? last.count : 0;
            return new Streak(category.Id, days, creditedToday, Math.Clamp(days - 1, 0, Progression.MaximumStreakBonusPercent));
        }).ToImmutableArray();
        var entitlements = Progression.Entitlements(surviving.Select(CompletionQuest), rank.Current);
        return new(
            Zone,
            Scheduling.DateText(today),
            views,
            overall,
            Progression.Level(overall, Track.Overall),
            Balances(Catalog.Categories, categoryXp, Track.Category),
            Balances(Catalog.Attributes, attributeXp, Track.Attribute),
            Balances(skillItems, skillXp, Track.Skill),
            rank,
            currentStreaks,
            entitlements,
            Definitions.ToImmutableArray(),
            Profile with {
                BadgeId = entitlements.Any(e => e.Id == Profile.BadgeId && e.Earned) ? Profile.BadgeId : null,
                FrameId = entitlements.Any(e => e.Id == Profile.FrameId && e.Earned) ? Profile.FrameId : null },
            Schedule,
            [.. penalties],
            [.. ledger],
            FutureWarnings(now));
    }

    private static ImmutableArray<Balance> Balances(ImmutableArray<NamedItem> items, Dictionary<string, long> xp, Track track) =>
        items.Select(x => new Balance(x.Id, x.Name, xp[x.Id], Progression.Level(xp[x.Id], track))).ToImmutableArray();

    private void ApplyCore(QuestCommand command, DateTimeOffset now)
    {
        Progression.Require(command.OperationId != Guid.Empty, "An operation ID is required.");
        switch (command.Action)
        {
            case QuestActions.Accept: Accept(command, now); break;
            case QuestActions.Complete: Complete(command, now); break;
            case QuestActions.Undo: Undo(command, now); break;
            case QuestActions.SaveDefinition: SaveDefinition(command, now); break;
            case QuestActions.ArchiveDefinition: ArchiveDefinition(command, now); break;
            case QuestActions.AssessSkill: AssessSkill(command, now); break;
            case QuestActions.SaveSkill: SaveSkill(command); break;
            case QuestActions.ArchiveSkill: ArchiveSkill(command); break;
            case QuestActions.Interests: SetInterests(command); break;
            case QuestActions.FinishOnboarding: Profile = Profile with { OnboardingComplete = true }; break;
            case QuestActions.Plan: Plan(command, now); break;
            case QuestActions.Zone: ChangeZone(command, now); break;
            case QuestActions.ExpiryWarnings: Profile = Profile with { ExpiryWarnings = command.ExpiryWarnings ?? throw new ArgumentException("Choose whether to enable expiry warnings.") }; break;
            case QuestActions.Link: Link(command); break;
            case QuestActions.SelectBadge: SelectReward(command, "Badge", now); break;
            case QuestActions.SelectFrame: SelectReward(command, "Frame", now); break;
            case QuestActions.SaveSeries: SaveSeries(command, now); break;
            case QuestActions.StopSeries: StopSeries(command, now); break;
            case QuestActions.Pause: Pause(command, now, true); break;
            case QuestActions.Resume: Pause(command, now, false); break;
            case QuestActions.ResumeOccurrence: ResumeOccurrence(command, now); break;
            case QuestActions.Abandon: Abandon(command, now); break;
            case QuestActions.AcceptOffer: AcceptOffer(command, now); break;
            default: throw new ArgumentException("Unknown quest action.");
        }
    }

    private void Accept(QuestCommand command, DateTimeOffset now)
    {
        var quest = Catalog.Quests.SingleOrDefault(q => q.Id == command.QuestId)
            ?? Definitions.SingleOrDefault(d => d.Quest.Id == command.QuestId && !d.Archived)?.Quest
            ?? throw new ArgumentException("Choose an available quest.");
        RequireEligible(quest, now);
        Progression.Require(!IsPaused(quest.CategoryId), "Resume this category before accepting a quest.");
        ValidatePlannedTime(command.DueDate, command.PlannedTime);
        Progression.Require(command.CompletionId is null && command.OccurrenceId != Guid.Empty, "Invalid occurrence or completion ID.");
        Progression.Require(command.OccurrenceId is null || Occurrences.All(o => o.Id != command.OccurrenceId), "Occurrence ID is already in use.");
        DateTimeOffset? deadline = null;
        if (command.DueDate is not null)
        {
            var date = Scheduling.ParseDate(command.DueDate);
            Progression.Require(date >= Scheduling.LocalDay(now, Zone), "Choose today or a future due date.");
            Progression.Require(date.Year <= 9998, "Choose a due date before year 9999.");
            deadline = Scheduling.Deadline(date, Zone);
        }

        var terms = (PenaltyTerms(quest, command, deadline) ?? new()) with { SourceAnchorId = command.RecordedTime?.AnchorId, DeadlineZone = Zone };
        Occurrences.Add(new(command.OccurrenceId ?? Guid.NewGuid(), quest, command.DueDate, deadline, now, command.PlannedTime, Lifecycle: terms));
    }

    private Occurrence Find(QuestCommand command)
    {
        Progression.Require(command.QuestId is null && command.DueDate is null, "Completion and Undo cannot change accepted terms.");
        return Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId)
            ?? throw new KeyNotFoundException("Quest occurrence was not found.");
    }

    private Completion? Surviving(Guid occurrenceId, DateTimeOffset? cutoff = null) => Completions.LastOrDefault(c => c.OccurrenceId == occurrenceId && (cutoff is null || c.RecordedAt <= cutoff) && Undos.All(u => u.CompletionId != c.Id || (cutoff is not null && u.RecordedAt > cutoff)));

    private void Complete(QuestCommand command, DateTimeOffset now)
    {
        var occurrence = Find(command);
        Progression.Require(command.CompletionId is null, "Completion ID is assigned by the server.");
        if (Surviving(occurrence.Id) is not null)
            return;
        Progression.Require(occurrence.Lifecycle?.Unaccepted != true && occurrence.Lifecycle?.FrozenAt is null && occurrence.Lifecycle?.AbandonedAt is null && !IsPaused(occurrence.Quest.CategoryId), "Resume a frozen quest before completion; abandoned quests cannot be completed.");
        if (now < occurrence.AcceptedAt || (occurrence.Deadline is { } deadline && now >= deadline))
            throw new InvalidOperationException("This quest is past its due-day deadline. Choose a new occurrence for a new action.");
        Completions.Add(new(command.OperationId, occurrence.Id, now, occurrence.Quest));
    }

    private void Undo(QuestCommand command, DateTimeOffset now)
    {
        var occurrence = Find(command);
        var completion = Completions.SingleOrDefault(c => c.Id == command.CompletionId && c.OccurrenceId == occurrence.Id)
            ?? throw new KeyNotFoundException("Completion was not found.");
        if (Undos.Any(u => u.CompletionId == completion.Id))
            return;
        if (now < completion.RecordedAt || now >= completion.RecordedAt.Add(QuestRules.UndoWindow))
            throw new InvalidOperationException("Undo is available for 24 elapsed hours after completion.");
        Undos.Add(new(command.OperationId, completion.Id, now));
        if (IsPaused(occurrence.Quest.CategoryId))
            FreezeOccurrence(occurrence, now, false);
        else if (Schedule.Series.Any(s => s.Id == occurrence.Lifecycle?.SeriesId && s.Stopped))
            FreezeOccurrence(occurrence, now, true);
    }
}
