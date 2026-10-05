using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using System.Collections.Immutable;

namespace PocketQuests.Services.Quests.Mapping;

internal static class QuestProfileMapping
{
    internal static PlayerProfile ToProfile(this QuestCompletionRows rows, AccountEntity account)
    {
        var keys = rows.Skills.ToDictionary(row => row.Id, row => row.ClientKey ?? row.Id.ToString("D"));
        var history = rows.Assessments.Select(row => new SkillAssessment(row.SystemSkillId ?? keys[row.CustomSkillId!.Value], Enum.Parse<Experience>(row.ExperienceCode), row.EffectiveAt)).ToImmutableList();
        var assessed = history.Aggregate(ImmutableDictionary<string, Experience>.Empty, (latest, assessment) => latest.SetItem(assessment.SkillId, assessment.Experience));
        return new(
            [.. rows.Interests.Select(row => row.CategoryId)],
            assessed,
            account.IsOnboardingComplete,
            account.SelectedBadgeId,
            account.SelectedFrameId,
            rows.Skills.Count == 0 ? null : [.. rows.Skills.Select(row => new CustomSkill(keys[row.Id], row.Name, row.Revision, row.ArchivedAt is not null))],
            history.Count == 0 ? null : history,
            rows.Zones.Count == 0 ? null : [.. rows.Zones.Select(row => new ZoneChange(row.FromTimeZoneId, row.ToTimeZoneId, row.EffectiveAt))],
            account.HasExpiryWarnings);
    }
}
