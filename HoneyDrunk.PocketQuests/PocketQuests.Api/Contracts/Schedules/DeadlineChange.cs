using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>The public DeadlineChange JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record DeadlineChange(Guid OccurrenceId, string Title, DateTimeOffset Deadline, bool BecomesMissed);
