using PocketQuests.Contracts.Enums.Progress;
using PocketQuests.Contracts.Models.Progress;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Projections;

/// <summary>The public CompletionOutcome JSON contract, independent of storage and domain behavior.</summary>
public sealed record CompletionOutcome(Guid CompletionId, Guid OccurrenceId,
    ImmutableArray<CompletionLevelUp> LevelUps, Rank? RankUp, ImmutableArray<Entitlement> Unlocks);
