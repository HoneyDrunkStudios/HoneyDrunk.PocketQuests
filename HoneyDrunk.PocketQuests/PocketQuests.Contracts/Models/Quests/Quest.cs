using PocketQuests.Contracts.Enums.Progress;
using PocketQuests.Contracts.Enums.Quests;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Models.Quests;

/// <summary>The public Quest JSON contract, independent of storage and domain behavior.</summary>
public sealed record Quest(string Id, string Title, string Criterion, string CategoryId, Rank Rank,
    Effort Effort, ImmutableArray<Share> Attributes, ImmutableArray<Share> Skills, bool IsCustom, string? Description, int PenaltyPercent, int BaseXp);
