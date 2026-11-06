using PocketQuests.Contracts.Enums.Profiles;

namespace PocketQuests.Contracts.Models.Profiles;

/// <summary>The public SkillAssessment JSON contract, independent of storage and domain behavior.</summary>
/// <param name="SkillId">The skill identifier.</param>
/// <param name="Experience">The experience represented by this result.</param>
/// <param name="At">The instant associated with this event or calculation.</param>
public sealed record SkillAssessment(string SkillId, Experience Experience, DateTimeOffset At);
