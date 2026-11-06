using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>Authoritative progress rebuilt from the account's surviving events.</summary>
/// <param name="Zone">The applicable account time zone.</param>
/// <param name="Today">The current date in the account time zone.</param>
/// <param name="Occurrences">The occurrences included in this snapshot.</param>
/// <param name="OverallXp">The total character experience.</param>
/// <param name="OverallLevel">The overall character progression level.</param>
/// <param name="Categories">The categories included in this snapshot.</param>
/// <param name="Attributes">The character attributes in this snapshot.</param>
/// <param name="Skills">The skills included in this snapshot.</param>
/// <param name="Rank">The quest or progression rank.</param>
/// <param name="Streaks">The streak projections included in this snapshot.</param>
/// <param name="Entitlements">The account entitlements in this snapshot.</param>
/// <param name="Definitions">The quest definitions included in this snapshot.</param>
/// <param name="Profile">The character profile used by this operation.</param>
/// <param name="Schedule">The recurrence schedule associated with this operation.</param>
/// <param name="Penalties">The penalties included in this result.</param>
/// <param name="Ledger">The retained experience ledger entries.</param>
/// <param name="FutureWarnings">The warnings concerning future scheduled occurrences.</param>
/// <param name="CompletionOutcome">The retained result of applying a completion.</param>
public record QuestState(
    string Zone,
    string Today,
    ImmutableArray<OccurrenceView> Occurrences,
    long OverallXp,
    int OverallLevel,
    ImmutableArray<Balance> Categories,
    ImmutableArray<Balance> Attributes,
    ImmutableArray<Balance> Skills,
    RankProgress Rank,
    ImmutableArray<Streak> Streaks,
    ImmutableArray<Entitlement> Entitlements,
    ImmutableArray<QuestDefinition> Definitions,
    PlayerProfile Profile,
    ScheduleState Schedule,
    ImmutableArray<PenaltyAssessment> Penalties,
    ImmutableArray<XpEntry> Ledger,
    ImmutableList<DateTimeOffset>? FutureWarnings = null,
    CompletionOutcome? CompletionOutcome = null);
