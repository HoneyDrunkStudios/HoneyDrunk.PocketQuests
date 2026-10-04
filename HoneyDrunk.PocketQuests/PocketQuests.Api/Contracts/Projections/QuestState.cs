using PocketQuests.Api.Contracts.Profiles;
using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Quests.Definitions;
using PocketQuests.Api.Contracts.Schedules;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Projections;

/// <summary>The public QuestState JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record QuestState(string Zone, string Today, ImmutableArray<OccurrenceView> Occurrences,
    long OverallXp, int OverallLevel, ImmutableArray<Balance> Categories, ImmutableArray<Balance> Attributes,
    ImmutableArray<Balance> Skills, RankProgress Rank, ImmutableArray<Streak> Streaks, ImmutableArray<Entitlement> Entitlements,
    ImmutableArray<QuestDefinition> Definitions, PlayerProfile Profile, ScheduleState Schedule, ImmutableArray<PenaltyAssessment> Penalties, ImmutableArray<XpEntry> Ledger, ImmutableList<DateTimeOffset>? FutureWarnings,
    CompletionOutcome? CompletionOutcome);
