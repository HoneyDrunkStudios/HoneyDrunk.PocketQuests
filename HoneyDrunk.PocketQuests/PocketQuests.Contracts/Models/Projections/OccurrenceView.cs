using PocketQuests.Contracts.Enums.Quests;
using PocketQuests.Contracts.Models.Quests;
using PocketQuests.Contracts.Models.Schedules;

namespace PocketQuests.Contracts.Models.Projections;

/// <summary>The public OccurrenceView JSON contract, independent of storage and domain behavior.</summary>
public sealed record OccurrenceView(Occurrence Occurrence, QuestStatus Status, Completion? Completion, bool CanUndo, PlannedMoment? Planned);
