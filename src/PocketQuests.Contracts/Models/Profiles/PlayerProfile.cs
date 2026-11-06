using PocketQuests.Contracts.Enums.Profiles;
using PocketQuests.Contracts.Models.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Profiles;

/// <summary>The public PlayerProfile JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Interests">The account interests included in this snapshot.</param>
/// <param name="Assessments">The skill assessments included in this result.</param>
/// <param name="OnboardingComplete">Whether the account has completed onboarding.</param>
/// <param name="BadgeId">The badge identifier.</param>
/// <param name="FrameId">The recorded-clock frame identifier.</param>
/// <param name="CustomSkills">The account-defined skills in this snapshot.</param>
/// <param name="AssessmentHistory">The retained skill assessment history.</param>
/// <param name="ZoneHistory">The retained account time-zone transitions.</param>
/// <param name="ExpiryWarnings">The warnings for expiring scheduled occurrences.</param>
/// <param name="CharacterName">The account character display name.</param>
/// <param name="PortraitId">The selected character portrait identifier, when present.</param>
/// <param name="AvatarCode">The selected character avatar code, when present.</param>
public sealed record PlayerProfile(
    ImmutableArray<string> Interests,
    ImmutableDictionary<string, Experience> Assessments,
    bool OnboardingComplete,
    string? BadgeId,
    string? FrameId,
    ImmutableList<CustomSkill>? CustomSkills,
    ImmutableList<SkillAssessment>? AssessmentHistory,
    ImmutableList<ZoneChange>? ZoneHistory,
    bool ExpiryWarnings,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CharacterName = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? PortraitId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? AvatarCode = null);
