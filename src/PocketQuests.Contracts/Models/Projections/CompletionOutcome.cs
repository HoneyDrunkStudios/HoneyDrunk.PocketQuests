using PocketQuests.Contracts.Enums.Progress;
using PocketQuests.Contracts.Models.Progress;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Projections;

/// <summary>The public CompletionOutcome JSON contract, independent of storage and domain behavior.</summary>
/// <param name="CompletionId">The completion identifier.</param>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="LevelUps">The level increases produced by this calculation.</param>
/// <param name="RankUp">The rank increase produced by this calculation.</param>
/// <param name="Unlocks">The unlocks produced by this progression result.</param>
public sealed record CompletionOutcome(
    Guid CompletionId,
    Guid OccurrenceId,
    ImmutableArray<CompletionLevelUp> LevelUps,
    Rank? RankUp,
    ImmutableArray<Entitlement> Unlocks);
