using PocketQuests.Api.Contracts.Quests.Events;
using PocketQuests.Api.Contracts.Quests.Occurrences;
using PocketQuests.Api.Contracts.Schedules;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Projections;

/// <summary>The public OccurrenceView JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record OccurrenceView(Occurrence Occurrence, QuestStatus Status, Completion? Completion, bool CanUndo, PlannedMoment? Planned);
