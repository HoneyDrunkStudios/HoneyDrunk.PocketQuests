using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Profiles;

/// <summary>The public SkillAssessment JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record SkillAssessment(string SkillId, Experience Experience, DateTimeOffset At);
