namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public PenaltyAssessment JSON contract, independent of storage and domain behavior.</summary>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="LockedLoss">The loss retained in the accepted terms.</param>
/// <param name="ActualLoss">The loss applied after the domain calculation.</param>
/// <param name="At">The instant associated with this event or calculation.</param>
public sealed record PenaltyAssessment(Guid OccurrenceId, string CategoryId, int LockedLoss, long ActualLoss, DateTimeOffset At);
