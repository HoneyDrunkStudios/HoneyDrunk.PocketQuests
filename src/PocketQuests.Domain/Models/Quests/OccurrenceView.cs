using PocketQuests.Domain.Models.Schedules;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>An occurrence with its current status and completion-specific Undo availability.</summary>
/// <param name="Occurrence">The occurrence included in this result.</param>
/// <param name="Status">The resource or operation status.</param>
/// <param name="Completion">The completion represented by this result.</param>
/// <param name="CanUndo">Whether the completion can currently be undone.</param>
/// <param name="Planned">The planned value or schedule for this result.</param>
public record OccurrenceView(Occurrence Occurrence, QuestStatus Status, Completion? Completion, bool CanUndo, PlannedMoment? Planned = null);
