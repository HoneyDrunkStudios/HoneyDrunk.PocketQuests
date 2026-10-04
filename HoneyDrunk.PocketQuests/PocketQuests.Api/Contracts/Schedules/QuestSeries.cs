using PocketQuests.Api.Contracts.Quests.Definitions;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Schedules;

/// <summary>The public QuestSeries JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record QuestSeries(Guid Id, Quest Quest, string Anchor, Cadence Cadence, int Interval,
    int Version, int NextSequence, int PauseDays, bool Stopped, string? PlannedTime, bool AutoAcceptPenalty, DateTimeOffset? EffectiveAt);
