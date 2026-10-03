using NodaTime;
using NodaTime.Text;
using PocketQuests.Domain.Progress;

namespace PocketQuests.Domain.Schedules;

/// <summary>Timezone-aware deadline and calendar recurrence calculations.</summary>
public static class Scheduling
{
    /// <summary>Parses an ISO local date without converting it through UTC.</summary>
    /// <param name="date">The local calendar date.</param>
    /// <returns>The validated local date.</returns>
    public static LocalDate ParseDate(string date)
    {
        var parsed = LocalDatePattern.Iso.Parse(date);
        Progression.Require(parsed.Success, "Use a valid date in YYYY-MM-DD format.");
        return parsed.Value;
    }

    /// <summary>Resolves a known IANA timezone.</summary>
    /// <param name="id">The stable timezone identifier.</param>
    /// <returns>The TZDB timezone definition.</returns>
    public static DateTimeZone Zone(string id) => DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)
        ?? throw new ArgumentException("Choose a valid IANA timezone.");

    /// <summary>Finds the first valid instant after the due day, including DST gaps and skipped dates.</summary>
    /// <param name="due">The inclusive local due date.</param>
    /// <param name="zoneId">The account's IANA timezone identifier.</param>
    /// <returns>The exclusive absolute completion deadline.</returns>
    public static DateTimeOffset Deadline(LocalDate due, string zoneId)
    {
        var zone = Zone(zoneId);
        var next = due.PlusDays(1);

        // AtStartOfDay uses the first occurrence at ambiguous midnight and the first
        // valid instant after a midnight gap. Entirely skipped dates advance again.
        while (true)
        {
            try
            {
                return zone.AtStartOfDay(next).ToDateTimeOffset();
            }
            catch (SkippedTimeException)
            {
                next = next.PlusDays(1);
            }
        }
    }

    /// <summary>Resolves a planned time to the next valid instant in a gap, or the first occurrence in an overlap.</summary>
    /// <param name="date">The planned local date.</param>
    /// <param name="time">The HH:mm planned time.</param>
    /// <param name="zoneId">The selected IANA timezone.</param>
    /// <returns>The resolved instant and visible adjustment.</returns>
    public static PlannedMoment Planned(string date, string time, string zoneId)
    {
        var parsed = LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(time);
        Progression.Require(parsed.Success, "Planned time must use HH:mm.");
        var local = ParseDate(date).At(parsed.Value);
        var zone = Zone(zoneId);
        var mapping = zone.MapLocal(local);
        var resolved = mapping.Count == 0 ? mapping.LateInterval.Start.InZone(zone) : mapping.First();
        return new(local.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), resolved.LocalDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), resolved.ToDateTimeOffset(), mapping.Count == 0, mapping.Count == 2);
    }

    /// <summary>Converts an instant to the account's local calendar day.</summary>
    /// <param name="instant">The absolute instant to convert.</param>
    /// <param name="zone">The account's IANA timezone identifier.</param>
    /// <returns>The local date.</returns>
    public static LocalDate LocalDay(DateTimeOffset instant, string zone) =>
        Instant.FromDateTimeOffset(instant).InZone(Zone(zone)).Date;

    /// <summary>Formats a local date as invariant ISO text.</summary>
    /// <param name="date">The local calendar date.</param>
    /// <returns>The YYYY-MM-DD date.</returns>
    public static string DateText(LocalDate date) => LocalDatePattern.Iso.Format(date);

    /// <summary>Calculates an occurrence from the original anchor, avoiding month and leap-year drift.</summary>
    /// <param name="anchor">The original local recurrence anchor.</param>
    /// <param name="cadence">The calendar unit.</param>
    /// <param name="interval">The positive interval between occurrences.</param>
    /// <param name="sequence">The zero-based occurrence number.</param>
    /// <param name="pauseDays">Nonnegative calendar-day offset from pauses.</param>
    /// <returns>The occurrence's local date after any pause offset.</returns>
    public static LocalDate Recurrence(LocalDate anchor, Cadence cadence, int interval, int sequence, int pauseDays = 0)
    {
        Progression.Require(interval is >= 1 and <= 999 && sequence >= 0 && pauseDays >= 0, "Invalid recurrence interval, sequence or pause offset.");
        var n = checked(interval * sequence);
        var original = cadence switch
        {
            Cadence.Days => anchor.PlusDays(n),
            Cadence.Weeks => anchor.PlusDays(checked(n * 7)),
            Cadence.Months => anchor.PlusMonths(n),
            Cadence.Years => anchor.PlusYears(n),
            _ => throw new ArgumentException("Invalid cadence.")
        };
        return original.PlusDays(pauseDays);
    }
}
