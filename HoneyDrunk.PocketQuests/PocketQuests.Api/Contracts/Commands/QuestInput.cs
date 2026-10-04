using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Quests.Definitions;
using System.Collections.Immutable;

namespace PocketQuests.Api.Contracts.Commands;

/// <summary>Accepted quest input, retaining optional defaults and numeric-string binding.</summary>
public sealed record QuestInput(string Id, string Title, string Criterion, string CategoryId, Rank Rank,
    Effort Effort, ImmutableArray<ShareInput> Attributes, ImmutableArray<ShareInput> Skills, bool IsCustom = false, string? Description = null, int PenaltyPercent = 0, int BaseXp = 0);
