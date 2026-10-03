using PocketQuests.Domain.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Profiles;

/// <summary>Durable preferences and replaceable assessment components, never earned XP.</summary>
public record PlayerProfile(ImmutableArray<string> Interests, ImmutableDictionary<string, Experience> Assessments,
    bool OnboardingComplete = false, string? BadgeId = null, string? FrameId = null, ImmutableList<CustomSkill>? CustomSkills = null, ImmutableList<SkillAssessment>? AssessmentHistory = null, ImmutableList<ZoneChange>? ZoneHistory = null, bool ExpiryWarnings = false)
{
    /// <summary>Gets the initial profile without selected interests or assessed experience.</summary>
    public static PlayerProfile Empty { get; } = new([], ImmutableDictionary<string, Experience>.Empty);
}
