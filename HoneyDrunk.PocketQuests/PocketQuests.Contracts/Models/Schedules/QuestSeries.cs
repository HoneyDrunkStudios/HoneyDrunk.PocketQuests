using PocketQuests.Contracts.Enums.Schedules;
using PocketQuests.Contracts.Models.Quests;

namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public QuestSeries JSON contract, independent of storage and domain behavior.</summary>
public sealed record QuestSeries(Guid Id, Quest Quest, string Anchor, Cadence Cadence, int Interval,
    int Version, int NextSequence, int PauseDays, bool Stopped, string? PlannedTime, bool AutoAcceptPenalty, DateTimeOffset? EffectiveAt);
