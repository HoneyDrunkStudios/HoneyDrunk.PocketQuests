namespace PocketQuests.Domain.Schedules;

/// <summary>Calendar recurrence units evaluated from the original anchor.</summary>
public enum Cadence
{
    /// <summary>An interval in local calendar days.</summary>
    Days,

    /// <summary>An interval in seven-day calendar weeks.</summary>
    Weeks,

    /// <summary>An interval in calendar months with short-month clamping.</summary>
    Months,

    /// <summary>An interval in calendar years with leap-day clamping.</summary>
    Years
}
