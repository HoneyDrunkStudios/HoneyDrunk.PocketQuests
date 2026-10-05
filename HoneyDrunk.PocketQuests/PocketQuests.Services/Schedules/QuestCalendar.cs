using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Schedules;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Schedules;

internal static class QuestCalendar
{
    internal static DateOnly? NextDelivery(QuestSeries series)
    {
        if (series.Stopped)
            return null;
        try
        {
            var date = Scheduling.Recurrence(Scheduling.ParseDate(series.Anchor), series.Cadence, series.Interval, series.NextSequence, series.PauseDays);
            return date.Year < 9999 ? QuestValues.Date(Scheduling.DateText(date)) : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
