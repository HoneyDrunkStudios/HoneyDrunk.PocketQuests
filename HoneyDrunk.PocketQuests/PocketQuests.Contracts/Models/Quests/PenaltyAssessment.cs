namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public PenaltyAssessment JSON contract, independent of storage and domain behavior.</summary>
public sealed record PenaltyAssessment(Guid OccurrenceId, string CategoryId, int LockedLoss, long ActualLoss, DateTimeOffset At);
