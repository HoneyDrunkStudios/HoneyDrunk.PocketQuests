using PocketQuests.Contracts.Enums.Profiles;

namespace PocketQuests.Contracts.Models.Profiles;

/// <summary>The public SkillAssessment JSON contract, independent of storage and domain behavior.</summary>
public sealed record SkillAssessment(string SkillId, Experience Experience, DateTimeOffset At);
