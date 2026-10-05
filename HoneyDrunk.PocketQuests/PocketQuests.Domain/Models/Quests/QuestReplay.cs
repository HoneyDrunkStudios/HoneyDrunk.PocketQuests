using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>The historical aggregate and completion feedback reconstructed at a committed version.</summary>
public sealed record QuestReplay(QuestAggregate Aggregate, CompletionOutcome? Outcome);
