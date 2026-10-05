using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Progress;
using QuestValues = PocketQuests.Domain.Services.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class CompletionProgressMapping
{
    internal static void ApplyTo(this QuestCommit change, QuestCompletionRows rows, QuestCompletionChanges changes)
    {
        ApplyLedger(change, rows, changes);
        ApplyBalances(change, rows, changes);
        ApplyCategories(change, rows, changes);
        ApplyEntitlements(change, rows, changes);
    }

    private static void ApplyLedger(QuestCommit change, QuestCompletionRows rows, QuestCompletionChanges changes)
    {
        var existing = rows.Ledger.ToDictionary(row => row.Id);
        var completions = change.Aggregate.Completions.ToDictionary(row => row.Id);
        var occurrences = rows.Occurrences.Concat(changes.Occurrences).ToDictionary(row => row.Id);
        var eventIds = rows.Events.Concat(changes.Events).Select(row => row.Id).ToHashSet();
        var desired = new HashSet<Guid>();
        foreach (var entry in change.State.Ledger)
        {
            var completion = completions.GetValueOrDefault(entry.EventId);
            var eventId = completion is null ? QuestValues.Derived(change.Account.Id, $"penalty/{entry.OccurrenceId:D}/{entry.At:O}") : entry.EventId;
            if (eventIds.Add(eventId))
                changes.Events.Add(CompletionOccurrenceMapping.ToEvent(change, eventId, entry.OccurrenceId, occurrences[entry.OccurrenceId].Revision, "DeadlineElapsed", entry.At));
            var amount = completion is not null && entry.Track == "Category" ? completion.Snapshot!.BaseXp : entry.Amount;
            ApplyContribution(eventId, completion is null ? "Penalty" : "Base", entry.Track, entry.TrackId, entry.At, amount);
            if (completion is not null && entry.Track == "Category" && entry.Amount != amount)
                ApplyContribution(eventId, "StreakBonus", entry.Track, entry.TrackId, entry.At, entry.Amount - amount);
        }

        changes.RemovedLedger.AddRange(rows.Ledger.Where(row => !desired.Contains(row.Id)));

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

            var (system, custom) = track == "Skill" ? QuestValues.Skill(target) : default;
            row.QuestOccurrenceEventId = eventId;
            row.ContributionCode = contribution;
            row.TrackCode = track;
            row.CategoryId = track == "Category" ? target : null;
            row.AttributeId = track == "Attribute" ? target : null;
            row.SystemSkillId = system;
            row.CustomSkillId = custom;
            row.EffectiveAt = at;
            row.Amount = amount;
            row.ProjectionVersion = change.Version;
            row.RulesetVersion = "1.0";
            row.ModifiedAt = row.ModifiedAt > change.Now ? row.ModifiedAt : change.Now;
        }
    }

    private static void ApplyBalances(QuestCommit change, QuestCompletionRows rows, QuestCompletionChanges changes)
    {
        var existing = rows.Balances.ToDictionary(row => row.Id);
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

        changes.RemovedBalances.AddRange(rows.Balances.Where(row => !desired.Contains(row.Id)));

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

            var (system, custom) = track == "Skill" ? QuestValues.Skill(target) : default;
            row.TrackCode = track;
            row.CategoryId = track == "Category" ? target : null;
            row.AttributeId = track == "Attribute" ? target : null;
            row.SystemSkillId = system;
            row.CustomSkillId = custom;
            row.EarnedXp = earned;
            row.SeedXp = seed;
            row.Level = level;
            row.ProjectionVersion = change.Version;
            row.ModifiedAt = row.ModifiedAt > change.Now ? row.ModifiedAt : change.Now;
        }
    }

    private static void ApplyCategories(QuestCommit change, QuestCompletionRows rows, QuestCompletionChanges changes)
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

            row.StreakDays = streak.Days;
            row.BonusRatePercent = streak.Rate;
            row.HasQualifiedToday = streak.QualifiedToday;
            row.IsExplicitlyPaused = change.Aggregate.Schedule.PausedCategories.Contains(streak.CategoryId);
            row.AsOfDate = QuestValues.Date(change.State.Today)!.Value;
            row.TimeZoneId = change.Aggregate.Zone;
            row.ProjectionVersion = change.Version;
            row.ModifiedAt = row.ModifiedAt > change.Now ? row.ModifiedAt : change.Now;
        }

        changes.RemovedCategories.AddRange(rows.CategoryProgress.Where(row => !desired.Contains(row.CategoryId)));
    }

    private static void ApplyEntitlements(QuestCommit change, QuestCompletionRows rows, QuestCompletionChanges changes)
    {
        var existing = rows.Entitlements.ToDictionary(row => row.ProfileRewardId);
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

            row.QualifyingCount = entitlement.Count;
            row.IsEarned = entitlement.Earned;
            row.ProjectionVersion = change.Version;
            row.RulesetVersion = "1.0";
            row.ModifiedAt = row.ModifiedAt > change.Now ? row.ModifiedAt : change.Now;
        }

        changes.RemovedEntitlements.AddRange(rows.Entitlements.Where(row => !desired.Contains(row.ProfileRewardId)));
    }
}
