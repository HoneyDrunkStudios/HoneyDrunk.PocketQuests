using PocketQuests.Contracts.Enums.Quests;
using PocketQuests.Contracts.Models.Quests;
using PocketQuests.Contracts.Models.Schedules;

namespace PocketQuests.Contracts.Models.Projections;

/// <summary>The public OccurrenceView JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Occurrence">The occurrence included in this result.</param>
/// <param name="Status">The resource or operation status.</param>
/// <param name="Completion">The completion represented by this result.</param>
/// <param name="CanUndo">Whether the completion can currently be undone.</param>
/// <param name="Planned">The planned value or schedule for this result.</param>
public sealed record OccurrenceView(Occurrence Occurrence, QuestStatus Status, Completion? Completion, bool CanUndo, PlannedMoment? Planned);
