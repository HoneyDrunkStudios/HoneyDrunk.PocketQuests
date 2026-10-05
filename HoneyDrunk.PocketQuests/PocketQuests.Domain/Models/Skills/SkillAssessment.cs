namespace PocketQuests.Domain.Models.Skills;

/// <summary>A replacement placement effective at the recorded instant, separate from earned XP.</summary>
public record SkillAssessment(string SkillId, Experience Experience, DateTimeOffset At);
