using PocketQuests.Services.Catalogs.Mapping;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Common.Mapping;
using PocketQuests.Services.Exports.Mapping;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Schedules.Mapping;
using PocketQuests.Services.Synchronization.Mapping;
using System.Collections.Immutable;

namespace PocketQuests.Services.Profiles.Mapping;

/// <summary>Explicit mappings for the profiles HTTP contracts.</summary>
public static class ProfilesContractMapping
{
    /// <summary>Maps CustomSkill explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Profiles.CustomSkill ToModel(this PocketQuests.Domain.Models.Skills.CustomSkill value) => new(
        value.Id,
        value.Name,
        value.Revision,
        value.Archived);

    /// <summary>Maps PlayerProfile explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Profiles.PlayerProfile ToModel(this PocketQuests.Domain.Models.Accounts.PlayerProfile value) => new(
        value.Interests,
        value.Assessments.ToImmutableDictionary(pair => pair.Key, pair => (PocketQuests.Contracts.Enums.Profiles.Experience)pair.Value),
        value.OnboardingComplete,
        value.BadgeId,
        value.FrameId,
        value.CustomSkills?.Select(item => item.ToModel()).ToImmutableList(),
        value.AssessmentHistory?.Select(item => item.ToModel()).ToImmutableList(),
        value.ZoneHistory?.Select(item => item.ToModel()).ToImmutableList(),
        value.ExpiryWarnings);

    /// <summary>Maps SkillAssessment explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Profiles.SkillAssessment ToModel(this PocketQuests.Domain.Models.Skills.SkillAssessment value) => new(
        value.SkillId,
        (PocketQuests.Contracts.Enums.Profiles.Experience)value.Experience,
        value.At);
}
