using PocketQuests.Api.Contracts.Quests.Definitions;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Quests.Events;

/// <summary>The public Completion JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record Completion(Guid Id, Guid OccurrenceId, DateTimeOffset RecordedAt, Quest? Snapshot);
