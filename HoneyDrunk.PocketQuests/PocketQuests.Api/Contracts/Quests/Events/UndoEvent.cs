using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Quests.Events;

/// <summary>The public UndoEvent JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record UndoEvent(Guid Id, Guid CompletionId, DateTimeOffset RecordedAt);
