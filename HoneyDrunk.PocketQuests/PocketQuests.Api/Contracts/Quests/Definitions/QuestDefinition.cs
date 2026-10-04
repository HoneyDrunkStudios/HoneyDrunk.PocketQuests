using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Quests.Definitions;

/// <summary>The public QuestDefinition JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record QuestDefinition(Quest Quest, int Revision, bool Archived);
