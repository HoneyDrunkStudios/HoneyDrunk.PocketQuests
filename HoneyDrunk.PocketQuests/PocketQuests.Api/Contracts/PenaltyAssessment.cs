using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts;

/// <summary>The public PenaltyAssessment JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record PenaltyAssessment(Guid OccurrenceId, string CategoryId, int LockedLoss, long ActualLoss, DateTimeOffset At);
