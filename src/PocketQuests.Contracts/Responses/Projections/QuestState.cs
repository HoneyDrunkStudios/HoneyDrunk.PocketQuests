using PocketQuests.Contracts.Models.Profiles;
using PocketQuests.Contracts.Models.Progress;
using PocketQuests.Contracts.Models.Projections;
using PocketQuests.Contracts.Models.Quests;
using PocketQuests.Contracts.Models.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Projections;

/// <summary>The public QuestState JSON contract, independent of storage and domain behavior.</summary>
public sealed record QuestState(string Zone, string Today, ImmutableArray<OccurrenceView> Occurrences,
    long OverallXp, int OverallLevel, ImmutableArray<Balance> Categories, ImmutableArray<Balance> Attributes,
    ImmutableArray<Balance> Skills, RankProgress Rank, ImmutableArray<Streak> Streaks, ImmutableArray<Entitlement> Entitlements,
    ImmutableArray<QuestDefinition> Definitions, PlayerProfile Profile, ScheduleState Schedule, ImmutableArray<PenaltyAssessment> Penalties, ImmutableArray<XpEntry> Ledger, ImmutableList<DateTimeOffset>? FutureWarnings,
    CompletionOutcome? CompletionOutcome);
