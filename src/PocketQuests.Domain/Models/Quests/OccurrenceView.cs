using PocketQuests.Domain.Models.Schedules;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>An occurrence with its current status and completion-specific Undo availability.</summary>
public record OccurrenceView(Occurrence Occurrence, QuestStatus Status, Completion? Completion, bool CanUndo, PlannedMoment? Planned = null);
