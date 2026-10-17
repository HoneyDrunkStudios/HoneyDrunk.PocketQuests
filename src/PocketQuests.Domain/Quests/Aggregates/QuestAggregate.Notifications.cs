using PocketQuests.Domain.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Quests.Aggregates;

/// <summary>Finite generic warning forecast; awards still require delivered, accepted occurrences.</summary>
public sealed partial class QuestAggregate
{
    private ImmutableList<DateTimeOffset> FutureWarnings(DateTimeOffset now)
    {
        if (!Profile.ExpiryWarnings || Schedule.AccountPaused || Schedule.Series.All(s => s.Stopped || IsPaused(s.Quest.CategoryId)))
            return [];
        var horizon = Scheduling.LocalDay(now, Zone).PlusDays(60);

        // Simulate delivery and missed penalties without forecasting future user actions.
        // Disabling warnings on the copy prevents recursive forecasts during reconciliation.
        var forecast = new QuestAggregate(Zone, Occurrences, Completions, Undos, Definitions, Profile with { ExpiryWarnings = false }, Schedule);
        forecast.Reconcile(Scheduling.Zone(Zone).AtStartOfDay(horizon).ToDateTimeOffset());
        var existing = Occurrences.Select(o => o.Id).ToHashSet();
        return [.. forecast.Occurrences.Where(o => !existing.Contains(o.Id) && o.Lifecycle?.Unaccepted != true
            && o.Lifecycle?.FrozenAt is null && o.Lifecycle?.AbandonedAt is null && o.Deadline > now)
            .Select(o => o.Deadline!.Value).Distinct().Order()];
    }
}
