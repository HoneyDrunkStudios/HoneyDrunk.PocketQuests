using PocketQuests.Api.Contracts.Catalogs;
using PocketQuests.Api.Contracts.Commands;
using PocketQuests.Api.Contracts.Common;
using PocketQuests.Api.Contracts.Exports;
using PocketQuests.Api.Contracts.Profiles;
using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Projections;
using PocketQuests.Api.Contracts.Quests;
using PocketQuests.Api.Contracts.Schedules;
using PocketQuests.Api.Contracts.Synchronization;
using System.Collections.Immutable;

namespace PocketQuests.Api.Contracts.Profiles;

/// <summary>Explicit mappings for the profiles HTTP contracts.</summary>
public static class ProfilesContractMapping
{
    /// <summary>Maps CustomSkill explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Profiles.CustomSkill ToContract(this PocketQuests.Domain.Models.Skills.CustomSkill value) => new(
        value.Id,
        value.Name,
        value.Revision,
        value.Archived);

    /// <summary>Maps PlayerProfile explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Profiles.PlayerProfile ToContract(this PocketQuests.Domain.Models.Accounts.PlayerProfile value) => new(
        value.Interests,
        value.Assessments.ToImmutableDictionary(pair => pair.Key, pair => (PocketQuests.Api.Contracts.Profiles.Experience)pair.Value),
        value.OnboardingComplete,
        value.BadgeId,
        value.FrameId,
        value.CustomSkills?.Select(item => item.ToContract()).ToImmutableList(),
        value.AssessmentHistory?.Select(item => item.ToContract()).ToImmutableList(),
        value.ZoneHistory?.Select(item => item.ToContract()).ToImmutableList(),
        value.ExpiryWarnings);

    /// <summary>Maps SkillAssessment explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Profiles.SkillAssessment ToContract(this PocketQuests.Domain.Models.Skills.SkillAssessment value) => new(
        value.SkillId,
        (PocketQuests.Api.Contracts.Profiles.Experience)value.Experience,
        value.At);
}
