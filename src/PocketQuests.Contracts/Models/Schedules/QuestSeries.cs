using PocketQuests.Contracts.Enums.Schedules;
using PocketQuests.Contracts.Models.Quests;

namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public QuestSeries JSON contract, independent of storage and domain behavior.</summary>
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
public sealed record QuestSeries(
    Guid Id,
    Quest Quest,
    string Anchor,
    Cadence Cadence,
    int Interval,
    int Version,
    int NextSequence,
    int PauseDays,
    bool Stopped,
    string? PlannedTime,
    bool AutoAcceptPenalty,
    DateTimeOffset? EffectiveAt);
