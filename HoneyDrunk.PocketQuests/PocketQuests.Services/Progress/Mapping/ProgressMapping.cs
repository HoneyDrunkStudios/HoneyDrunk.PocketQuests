using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Services.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Progress.Mapping;

internal static class ProgressMapping
{
    internal static XpLedgerEntryEntity ToLedger(Guid id, Guid accountId) => new() { Id = id, AccountId = accountId };

    internal static XpBalanceEntity ToBalance(Guid id, Guid accountId) => new() { Id = id, AccountId = accountId };

    internal static CategoryProgressEntity ToCategory(Guid id, Guid accountId, string categoryId) =>
        new() { Id = id, AccountId = accountId, CategoryId = categoryId };

    internal static AccountEntitlementEntity ToEntitlement(Guid id, Guid accountId, string rewardId) =>
        new() { Id = id, AccountId = accountId, ProfileRewardId = rewardId };

    internal static void ApplyLedger(XpLedgerEntryEntity row, QuestMutation change, Guid eventId, string contribution, string track, string target, DateTimeOffset at, long amount)
    {
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
    }

    internal static void ApplyBalance(XpBalanceEntity row, QuestMutation change, string track, string target, long earned, int level, long seed)
    {
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
    }

    internal static void ApplyCategory(CategoryProgressEntity row, QuestMutation change, Streak streak, bool paused)
    {
        row.StreakDays = streak.Days;
        row.BonusRatePercent = streak.Rate;
        row.HasQualifiedToday = streak.QualifiedToday;
        row.IsExplicitlyPaused = paused;
        row.AsOfDate = QuestValues.Date(change.State.Today)!.Value;
        row.TimeZoneId = change.Aggregate.Zone;
        row.ProjectionVersion = change.Version;
    }

    internal static void ApplyEntitlement(AccountEntitlementEntity row, QuestMutation change, Entitlement entitlement)
    {
        row.QualifyingCount = entitlement.Count;
        row.IsEarned = entitlement.Earned;
        row.ProjectionVersion = change.Version;
        row.RulesetVersion = "1.0";
    }
}
