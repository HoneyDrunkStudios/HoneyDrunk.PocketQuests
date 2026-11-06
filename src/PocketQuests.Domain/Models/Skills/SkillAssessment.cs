namespace PocketQuests.Domain.Models.Skills;

/// <summary>A replacement placement effective at the recorded instant, separate from earned XP.</summary>
/// <param name="SkillId">The skill identifier.</param>
/// <param name="Experience">The experience represented by this result.</param>
/// <param name="At">The instant associated with this event or calculation.</param>
public record SkillAssessment(string SkillId, Experience Experience, DateTimeOffset At);
