using PocketQuests.Domain.Quests.Definitions;

namespace PocketQuests.Domain.Schedules;

/// <summary>A durable calendar schedule; delivery sequence and original anchor survive retries and pauses.</summary>
public record QuestSeries(Guid Id, Quest Quest, string Anchor, Cadence Cadence, int Interval,
    int Version = 1, int NextSequence = 0, int PauseDays = 0, bool Stopped = false, string? PlannedTime = null, bool AutoAcceptPenalty = false, DateTimeOffset? EffectiveAt = null);
