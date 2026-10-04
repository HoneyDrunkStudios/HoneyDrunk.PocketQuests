using PocketQuests.Api.Contracts.Progress;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Quests.Definitions;

/// <summary>The public Quest JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record Quest(string Id, string Title, string Criterion, string CategoryId, Rank Rank,
    Effort Effort, ImmutableArray<Share> Attributes, ImmutableArray<Share> Skills, bool IsCustom, string? Description, int PenaltyPercent, int BaseXp);
