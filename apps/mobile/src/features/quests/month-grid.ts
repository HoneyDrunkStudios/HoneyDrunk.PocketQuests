import { isCalendarDate, shiftCalendarDate } from "./calendar-plan";

export function monthDates(month: string): (string | null)[] {
  const first = `${month.slice(0, 7)}-01`;
  if (!isCalendarDate(first)) return [];
  const offset = new Date(`${first}T00:00:00Z`).getUTCDay();
  const dates = Array.from({ length: 42 }, (_, index) => {
    const date = shiftCalendarDate(first, index - offset);
    return date?.slice(0, 7) === first.slice(0, 7) ? date : null;
  });
  while (dates.length > 28 && dates.slice(-7).every((date) => date === null)) dates.splice(-7);
  return dates;
}

export function adjacentMonth(month: string, direction: -1 | 1): string | null {
  const first = `${month.slice(0, 7)}-01`;
  if (!isCalendarDate(first)) return null;
  const near = shiftCalendarDate(first, direction === -1 ? -1 : 32);
  return near ? `${near.slice(0, 7)}-01` : null;
}
