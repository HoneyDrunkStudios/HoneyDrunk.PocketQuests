using PocketQuests.Contracts.Enums.Profiles;
using PocketQuests.Contracts.Models.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Profiles;

/// <summary>The public PlayerProfile JSON contract, independent of storage and domain behavior.</summary>
public sealed record PlayerProfile(ImmutableArray<string> Interests, ImmutableDictionary<string, Experience> Assessments,
    bool OnboardingComplete, string? BadgeId, string? FrameId, ImmutableList<CustomSkill>? CustomSkills, ImmutableList<SkillAssessment>? AssessmentHistory, ImmutableList<ZoneChange>? ZoneHistory, bool ExpiryWarnings);
