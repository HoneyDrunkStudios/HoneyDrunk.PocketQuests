namespace PocketQuests.Domain.Models.Quests;

/// <summary>A chronological category-only assessment, including its actual zero-floor-clamped loss.</summary>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="LockedLoss">The loss retained in the accepted terms.</param>
/// <param name="ActualLoss">The loss applied after the domain calculation.</param>
/// <param name="At">The instant associated with this event or calculation.</param>
public record PenaltyAssessment(Guid OccurrenceId, string CategoryId, int LockedLoss, long ActualLoss, DateTimeOffset At);
