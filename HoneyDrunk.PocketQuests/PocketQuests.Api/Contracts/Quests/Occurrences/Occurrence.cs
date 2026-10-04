using PocketQuests.Api.Contracts.Quests.Definitions;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Quests.Occurrences;

/// <summary>The public Occurrence JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record Occurrence(Guid Id, Quest Quest, string? DueDate, DateTimeOffset? Deadline, DateTimeOffset AcceptedAt, string? PlannedTime, Guid? ParentId, OccurrenceLifecycle? Lifecycle);
