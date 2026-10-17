using PocketQuests.Contracts.Models.Schedules;

namespace PocketQuests.Services.Schedules;

/// <summary>Calendar previews without mutation.</summary>
public interface IPlanningService
{
    /// <summary>Resolves the existing daylight-saving calendar rules.</summary>
    /// <param name="date">Local date.</param>
    /// <param name="time">Requested local time.</param>
    /// <param name="zone">IANA time zone.</param>
    /// <returns>The resolved moment and adjustment flags.</returns>
    PlannedMoment PlanClock(string date, string time, string zone);

    /// <summary>Previews pending deadlines under a proposed time zone.</summary>
    /// <param name="zone">Proposed IANA time zone.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Reviewable changes to active deadlines.</returns>
    Task<ZonePreview> PreviewZone(string zone, CancellationToken token = default);
}
