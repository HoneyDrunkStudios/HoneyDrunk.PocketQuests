import type { OccurrenceView } from "../../shared/contracts";
import { isCalendarDate } from "./calendar-plan";

export function planLockReason(item: OccurrenceView): string | null {
  if (item.status !== "Active")
    return "Only active commitments can be replanned. Completed and past plans stay in your history; frozen commitments must be resumed first.";
  if (item.occurrence.lifecycle?.seriesId)
    return "This occurrence belongs to a recurring series. Its delivery date cannot be changed here.";
  if (item.occurrence.lifecycle?.lockedLoss != null)
    return "This commitment has accepted penalty terms. Its date stays locked to those terms.";
  return null;
}

export function planInputError(
  date: string,
  time: string,
  today: string,
): string | null {
  if (date && !isCalendarDate(date))
    return "Enter a real due date in YYYY-MM-DD format.";
  if (date && date < today) return "Choose today or a future due date.";
  if (date >= "9999-01-01") return "Choose a due date before year 9999.";
  if (time && !date)
    return "A planned time needs a due date. Leave both blank for an unscheduled quest.";
  if (time && !/^([01]\d|2[0-3]):[0-5]\d$/.test(time))
    return "Enter a planned time from 00:00 to 23:59 in HH:mm format.";
  return null;
}
