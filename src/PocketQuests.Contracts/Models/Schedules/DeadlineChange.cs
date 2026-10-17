namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public DeadlineChange JSON contract, independent of storage and domain behavior.</summary>
public sealed record DeadlineChange(Guid OccurrenceId, string Title, DateTimeOffset Deadline, bool BecomesMissed);
