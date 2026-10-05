using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Progress;
using PocketQuests.Services.Quests.Mapping;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests;

internal static class ProgressChanges
{
    internal static void Apply(QuestMutation change, QuestStateRows rows, QuestProjectionRows projections, QuestChanges changes)
    {
        ApplyLedger(change, rows, projections, changes);
        ApplyBalances(change, rows, projections, changes);
        ApplyCategories(change, rows, projections, changes);
        ApplyEntitlements(change, rows, projections, changes);
    }

    private static void ApplyLedger(QuestMutation change, QuestStateRows rows, QuestProjectionRows projections, QuestChanges changes)
    {
        var existing = projections.Ledger.ToDictionary(row => row.Id);
        var completions = change.Aggregate.Completions.ToDictionary(row => row.Id);
        var occurrences = rows.Occurrences.Concat(changes.Occurrences).ToDictionary(row => row.Id);
        var eventIds = rows.Events.Concat(changes.Events).Select(row => row.Id).ToHashSet();
        var desired = new HashSet<Guid>();
        foreach (var entry in change.State.Ledger)
        {
            var completion = completions.GetValueOrDefault(entry.EventId);
            var eventId = completion is null ? QuestValues.Derived(change.Account.Id, $"penalty/{entry.OccurrenceId:D}/{entry.At:O}") : entry.EventId;
            if (eventIds.Add(eventId))
                changes.Events.Add(OccurrenceMapping.ToEvent(change, eventId, entry.OccurrenceId, occurrences[entry.OccurrenceId].Revision, "DeadlineElapsed", entry.At));
            var amount = completion is not null && entry.Track == "Category" ? completion.Snapshot!.BaseXp : entry.Amount;
            ApplyContribution(eventId, completion is null ? "Penalty" : "Base", entry.Track, entry.TrackId, entry.At, amount);
            if (completion is not null && entry.Track == "Category" && entry.Amount != amount)
                ApplyContribution(eventId, "StreakBonus", entry.Track, entry.TrackId, entry.At, entry.Amount - amount);
        }

        changes.RemovedLedger.AddRange(projections.Ledger.Where(row => !desired.Contains(row.Id)));

        void ApplyContribution(Guid eventId, string contribution, string track, string target, DateTimeOffset at, long amount)
        {
            var id = QuestValues.Derived(change.Account.Id, $"ledger/{eventId:D}/{contribution}/{track}/{target}");
            desired.Add(id);
            var row = existing.GetValueOrDefault(id);
            if (row is null)
            {
                row = new() { Id = id, AccountId = change.Account.Id, CreatedAt = change.Now };
                changes.Ledger.Add(row);
            }

            ProgressMapping.ApplyLedger(row, change, eventId, contribution, track, target, at, amount);
        }
    }

    private static void ApplyBalances(QuestMutation change, QuestStateRows rows, QuestProjectionRows projections, QuestChanges changes)
    {
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

        changes.RemovedBalances.AddRange(projections.Balances.Where(row => !desired.Contains(row.Id)));

        void ApplyBalance(string track, string target, long earned, int level, long seed)
        {
            var id = QuestValues.Derived(change.Account.Id, $"balance/{track}/{target}");
            desired.Add(id);
            var row = existing.GetValueOrDefault(id);
            if (row is null)
            {
                row = new() { Id = id, AccountId = change.Account.Id, CreatedAt = change.Now };
                changes.Balances.Add(row);
            }

            ProgressMapping.ApplyBalance(row, change, track, target, earned, level, seed);
        }
    }

    private static void ApplyCategories(QuestMutation change, QuestStateRows rows, QuestProjectionRows projections, QuestChanges changes)
    {
        var existing = rows.CategoryProgress.ToDictionary(row => row.CategoryId);
        var desired = new HashSet<string>();
        foreach (var streak in change.State.Streaks)
        {
            desired.Add(streak.CategoryId);
            var row = existing.GetValueOrDefault(streak.CategoryId);
            if (row is null)
            {
                row = new CategoryProgressEntity
                {
                    Id = QuestValues.Derived(change.Account.Id, "streak/" + streak.CategoryId),
                    AccountId = change.Account.Id,
                    CategoryId = streak.CategoryId,
                    CreatedAt = change.Now,
                };
                changes.Categories.Add(row);
            }

            ProgressMapping.ApplyCategory(row, change, streak, change.Aggregate.Schedule.PausedCategories.Contains(streak.CategoryId));
        }

        changes.RemovedCategories.AddRange(rows.CategoryProgress.Where(row => !desired.Contains(row.CategoryId)));
    }

    private static void ApplyEntitlements(QuestMutation change, QuestStateRows rows, QuestProjectionRows projections, QuestChanges changes)
    {
        var existing = projections.Entitlements.ToDictionary(row => row.ProfileRewardId);
        var desired = new HashSet<string>();
        foreach (var entitlement in change.State.Entitlements)
        {
            desired.Add(entitlement.Id);
            var row = existing.GetValueOrDefault(entitlement.Id);
            if (row is null)
            {
                row = new AccountEntitlementEntity
                {
                    Id = QuestValues.Derived(change.Account.Id, "reward/" + entitlement.Id),
                    AccountId = change.Account.Id,
                    ProfileRewardId = entitlement.Id,
                    CreatedAt = change.Now,
                };
                changes.Entitlements.Add(row);
            }

            ProgressMapping.ApplyEntitlement(row, change, entitlement);
        }

        changes.RemovedEntitlements.AddRange(projections.Entitlements.Where(row => !desired.Contains(row.ProfileRewardId)));
    }
}
