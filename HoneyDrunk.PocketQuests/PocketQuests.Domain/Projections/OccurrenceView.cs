using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Domain.Projections;

/// <summary>An occurrence with its current status and completion-specific Undo availability.</summary>
public record OccurrenceView(Occurrence Occurrence, QuestStatus Status, Completion? Completion, bool CanUndo, PlannedMoment? Planned = null);
