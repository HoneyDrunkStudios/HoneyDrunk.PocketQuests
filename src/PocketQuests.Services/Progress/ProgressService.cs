using PocketQuests.Data.DataServices.Categories;
using PocketQuests.Data.DataServices.Progress;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Progress;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Quests.Mapping;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Progress;

internal sealed class ProgressService(IXpLedgerEntryDataService ledgerData, IXpBalanceDataService balanceData,
    ICategoryProgressDataService categoryData, IAccountEntitlementDataService entitlementData, IQuestOccurrenceEventDataService eventData)
{
    internal async Task Recalculate(QuestMutation change, QuestStateRows rows, QuestProjectionRows projections, IReadOnlyList<QuestOccurrenceEntity> occurrences, IReadOnlySet<Guid> eventIds, CancellationToken token)
    {
        await ApplyLedger(change, projections, occurrences, eventIds, token);
        await ApplyBalances(change, projections, token);
        await ApplyCategories(change, rows, token);
        await ApplyEntitlements(change, projections, token);
    }

    private async Task ApplyLedger(QuestMutation change, QuestProjectionRows projections, IReadOnlyList<QuestOccurrenceEntity> occurrenceRows, IReadOnlySet<Guid> retainedEvents, CancellationToken token)
    {
        var existing = projections.Ledger.ToDictionary(row => row.Id);
        var completions = change.Aggregate.Completions.ToDictionary(row => row.Id);
        List<XpLedgerEntryEntity> newLedger = [];
        List<QuestOccurrenceEventEntity> newEvents = [];
        var occurrences = occurrenceRows.ToDictionary(row => row.Id);
        var eventIds = retainedEvents.ToHashSet();
        var desired = new HashSet<Guid>();
        foreach (var entry in change.State.Ledger)
        {
            var completion = completions.GetValueOrDefault(entry.EventId);
            var eventId = completion is null ? QuestValues.Derived(change.Account.Id, $"penalty/{entry.OccurrenceId:D}/{entry.At:O}") : entry.EventId;
            if (eventIds.Add(eventId))
                newEvents.Add(OccurrenceMapping.ToEvent(change, eventId, entry.OccurrenceId, occurrences[entry.OccurrenceId].Revision, "DeadlineElapsed", entry.At));
            var amount = completion is not null && entry.Track == "Category" ? completion.Snapshot!.BaseXp : entry.Amount;
            ApplyContribution(eventId, completion is null ? "Penalty" : "Base", entry.Track, entry.TrackId, entry.At, amount);
            if (completion is not null && entry.Track == "Category" && entry.Amount != amount)
                ApplyContribution(eventId, "StreakBonus", entry.Track, entry.TrackId, entry.At, entry.Amount - amount);
        }

        ledgerData.RemoveRange(projections.Ledger.Where(row => !desired.Contains(row.Id)));
        await ledgerData.AddRangeAsync(newLedger, token);
        foreach (var row in newEvents)
            row.CreatedAt = change.Now;
        await eventData.AddRangeAsync(newEvents, token);

        void ApplyContribution(Guid eventId, string contribution, string track, string target, DateTimeOffset at, long amount)
        {
            var id = QuestValues.Derived(change.Account.Id, $"ledger/{eventId:D}/{contribution}/{track}/{target}");
            desired.Add(id);
            var row = existing.GetValueOrDefault(id);
            if (row is null)
            {
                row = ProgressMapping.ToLedger(id, change.Account.Id);
                row.CreatedAt = change.Now;
                newLedger.Add(row);
            }

            ProgressMapping.ApplyLedger(row, change, eventId, contribution, track, target, at, amount);
            row.ModifiedAt = QuestClock.Max(row.ModifiedAt, change.Now);
        }
    }

    private async Task ApplyBalances(QuestMutation change, QuestProjectionRows projections, CancellationToken token)
    {
        List<XpBalanceEntity> newBalances = [];
        var existing = projections.Balances.ToDictionary(row => row.Id);
        var desired = new HashSet<Guid>();
        ApplyBalance("Overall", "overall", change.State.OverallXp, change.State.OverallLevel, 0);
        var assessments = change.Aggregate.Profile.AssessmentHistory?.Where(item => item.At <= change.ProjectionAt)
            .GroupBy(item => item.SkillId).ToDictionary(group => group.Key, group => group.OrderBy(item => item.At).Last());
        foreach (var (track, values) in new[] { (track: "Category", values: change.State.Categories), (track: "Attribute", values: change.State.Attributes), (track: "Skill", values: change.State.Skills) })
            foreach (var balance in values)
            {
                var assessment = track == "Skill" ? assessments?.GetValueOrDefault(balance.Id) : null;
                var seed = assessment is null ? 0 : Progression.Seed(assessment.Experience);
                ApplyBalance(track, balance.Id, balance.Xp - seed, balance.Level, seed);
            }

        balanceData.RemoveRange(projections.Balances.Where(row => !desired.Contains(row.Id)));
        await balanceData.AddRangeAsync(newBalances, token);

        void ApplyBalance(string track, string target, long earned, int level, long seed)
        {
            var id = QuestValues.Derived(change.Account.Id, $"balance/{track}/{target}");
            desired.Add(id);
            var row = existing.GetValueOrDefault(id);
            if (row is null)
            {
                row = ProgressMapping.ToBalance(id, change.Account.Id);
                row.CreatedAt = change.Now;
                newBalances.Add(row);
            }

            ProgressMapping.ApplyBalance(row, change, track, target, earned, level, seed);
            row.ModifiedAt = QuestClock.Max(row.ModifiedAt, change.Now);
        }
    }

    private async Task ApplyCategories(QuestMutation change, QuestStateRows rows, CancellationToken token)
    {
        List<CategoryProgressEntity> newCategories = [];
        var existing = rows.CategoryProgress.ToDictionary(row => row.CategoryId);
        var desired = new HashSet<string>();
        foreach (var streak in change.State.Streaks)
        {
            desired.Add(streak.CategoryId);
            var row = existing.GetValueOrDefault(streak.CategoryId);
            if (row is null)
            {
                row = ProgressMapping.ToCategory(QuestValues.Derived(change.Account.Id, "streak/" + streak.CategoryId), change.Account.Id, streak.CategoryId);
                row.CreatedAt = change.Now;
                newCategories.Add(row);
            }

            ProgressMapping.ApplyCategory(row, change, streak, change.Aggregate.Schedule.PausedCategories.Contains(streak.CategoryId));
            row.ModifiedAt = QuestClock.Max(row.ModifiedAt, change.Now);
        }

        categoryData.RemoveRange(rows.CategoryProgress.Where(row => !desired.Contains(row.CategoryId)));
        await categoryData.AddRangeAsync(newCategories, token);
    }

    private async Task ApplyEntitlements(QuestMutation change, QuestProjectionRows projections, CancellationToken token)
    {
        List<AccountEntitlementEntity> newEntitlements = [];
        var existing = projections.Entitlements.ToDictionary(row => row.ProfileRewardId);
        var desired = new HashSet<string>();
        foreach (var entitlement in change.State.Entitlements)
        {
            desired.Add(entitlement.Id);
            var row = existing.GetValueOrDefault(entitlement.Id);
            if (row is null)
            {
                row = ProgressMapping.ToEntitlement(QuestValues.Derived(change.Account.Id, "reward/" + entitlement.Id), change.Account.Id, entitlement.Id);
                row.CreatedAt = change.Now;
                newEntitlements.Add(row);
            }

            ProgressMapping.ApplyEntitlement(row, change, entitlement);
            row.ModifiedAt = QuestClock.Max(row.ModifiedAt, change.Now);
        }

        entitlementData.RemoveRange(projections.Entitlements.Where(row => !desired.Contains(row.ProfileRewardId)));
        await entitlementData.AddRangeAsync(newEntitlements, token);
    }
}
