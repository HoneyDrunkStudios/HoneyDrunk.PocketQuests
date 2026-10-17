using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>Authoritative progress rebuilt from the account's surviving events.</summary>
public record QuestState(string Zone, string Today, ImmutableArray<OccurrenceView> Occurrences,
    long OverallXp, int OverallLevel, ImmutableArray<Balance> Categories, ImmutableArray<Balance> Attributes,
    ImmutableArray<Balance> Skills, RankProgress Rank, ImmutableArray<Streak> Streaks, ImmutableArray<Entitlement> Entitlements,
    ImmutableArray<QuestDefinition> Definitions, PlayerProfile Profile, ScheduleState Schedule, ImmutableArray<PenaltyAssessment> Penalties, ImmutableArray<XpEntry> Ledger, ImmutableList<DateTimeOffset>? FutureWarnings = null,
    CompletionOutcome? CompletionOutcome = null);
