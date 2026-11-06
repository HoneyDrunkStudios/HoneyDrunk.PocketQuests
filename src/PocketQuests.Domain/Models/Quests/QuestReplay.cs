using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>The historical aggregate and completion feedback reconstructed at a committed version.</summary>
/// <param name="Aggregate">The quest aggregate reconstructed for this result.</param>
/// <param name="Outcome">The result of applying the operation.</param>
public sealed record QuestReplay(QuestAggregate Aggregate, CompletionOutcome? Outcome);
