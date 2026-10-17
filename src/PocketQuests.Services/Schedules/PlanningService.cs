using PocketQuests.Contracts.Enums.Quests;
using PocketQuests.Contracts.Models.Schedules;
using PocketQuests.Domain.Schedules;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Schedules.Mapping;

namespace PocketQuests.Services.Schedules;

/// <summary>Applies pure calendar rules to the current authenticated source projection.</summary>
/// <param name="quests">Current source-derived state.</param>
/// <param name="clock">Host time.</param>
public sealed class PlanningService(IQuestService quests, TimeProvider clock) : IPlanningService
{
    /// <inheritdoc />
    public PlannedMoment PlanClock(string date, string time, string zone) => Scheduling.Planned(date, time, zone).ToModel();

    /// <inheritdoc />
    public async Task<ZonePreview> PreviewZone(string zone, CancellationToken token = default)
    {
        var selected = Scheduling.Zone(zone).Id;
        var state = await quests.Read(token);
        var now = clock.GetUtcNow();
        var pending = state.Occurrences.Where(row => (row.Status is QuestStatus.Active or QuestStatus.Frozen or QuestStatus.Offered)
            && row.Occurrence.DueDate is not null && (row.Status == QuestStatus.Frozen || row.Occurrence.Deadline is null || row.Occurrence.Deadline > now));
        return new(state.Zone, selected, [.. pending.Select(row =>
        {
            var deadline = Scheduling.Deadline(Scheduling.ParseDate(row.Occurrence.DueDate!), selected);
            return new DeadlineChange(row.Occurrence.Id, row.Occurrence.Quest.Title, deadline, row.Status == QuestStatus.Active && deadline <= now);
        })]);
    }
}
