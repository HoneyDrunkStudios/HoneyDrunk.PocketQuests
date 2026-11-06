using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Models.Schedules;

/// <summary>A durable calendar schedule; delivery sequence and original anchor survive retries and pauses.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Quest">The quest associated with this operation.</param>
/// <param name="Anchor">The recorded-clock anchor associated with the operation.</param>
/// <param name="Cadence">The recurrence cadence.</param>
/// <param name="Interval">The number of cadence units between recurrences.</param>
/// <param name="Version">The version of the represented state or resource.</param>
/// <param name="NextSequence">The next event sequence number.</param>
/// <param name="PauseDays">The requested pause duration in days.</param>
/// <param name="Stopped">Whether this recurring series is stopped.</param>
/// <param name="PlannedTime">The planned occurrence time.</param>
/// <param name="AutoAcceptPenalty">Whether the caller accepts the associated penalty automatically.</param>
/// <param name="EffectiveAt">The instant at which this state takes effect.</param>
public record QuestSeries(
    Guid Id,
    Quest Quest,
    string Anchor,
    Cadence Cadence,
    int Interval,
    int Version = 1,
    int NextSequence = 0,
    int PauseDays = 0,
    bool Stopped = false,
    string? PlannedTime = null,
    bool AutoAcceptPenalty = false,
    DateTimeOffset? EffectiveAt = null);
