namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public PauseWindow JSON contract, independent of storage and domain behavior.</summary>
public sealed record PauseWindow(string CategoryId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
