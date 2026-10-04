using PocketQuests.Api.Contracts.Schedules;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Profiles;

/// <summary>The public PlayerProfile JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record PlayerProfile(ImmutableArray<string> Interests, ImmutableDictionary<string, Experience> Assessments,
    bool OnboardingComplete, string? BadgeId, string? FrameId, ImmutableList<CustomSkill>? CustomSkills, ImmutableList<SkillAssessment>? AssessmentHistory, ImmutableList<ZoneChange>? ZoneHistory, bool ExpiryWarnings);
