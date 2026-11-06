namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public PauseWindow JSON contract, independent of storage and domain behavior.</summary>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="StartedAt">The beginning timestamp.</param>
/// <param name="EndedAt">The end timestamp, when present.</param>
public sealed record PauseWindow(string CategoryId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
