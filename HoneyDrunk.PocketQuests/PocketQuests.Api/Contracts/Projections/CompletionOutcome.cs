using PocketQuests.Api.Contracts.Progress;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Projections;

/// <summary>The public CompletionOutcome JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record CompletionOutcome(Guid CompletionId, Guid OccurrenceId,
    ImmutableArray<CompletionLevelUp> LevelUps, Rank? RankUp, ImmutableArray<Entitlement> Unlocks);
