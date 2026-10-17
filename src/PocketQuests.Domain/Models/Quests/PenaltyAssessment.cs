namespace PocketQuests.Domain.Models.Quests;

/// <summary>A chronological category-only assessment, including its actual zero-floor-clamped loss.</summary>
public record PenaltyAssessment(Guid OccurrenceId, string CategoryId, int LockedLoss, long ActualLoss, DateTimeOffset At);
