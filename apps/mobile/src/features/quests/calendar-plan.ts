import type { OccurrenceView } from "../../shared/contracts";

const dayMilliseconds = 86_400_000;

// Selected-zone dates, not device-local instants. UTC is only an arithmetic
// container; the API continues to own actual deadlines.
export function isCalendarDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value < "0001-01-01") return false;
  const instant = Date.parse(`${value}T00:00:00Z`);
  return (
    Number.isFinite(instant) &&
    new Date(instant).toISOString().slice(0, 10) === value
  );
}

export function shiftCalendarDate(value: string, days: number): string | null {
  if (!isCalendarDate(value) || !Number.isInteger(days)) return null;
  const instant = Date.parse(`${value}T00:00:00Z`) + days * dayMilliseconds;
  if (!Number.isFinite(instant) || Math.abs(instant) > 8.64e15) return null;
  const result = new Date(instant).toISOString().slice(0, 10);
  return isCalendarDate(result) ? result : null;
}

export function canPlanCalendarDate(date: string, today: string): boolean {
  return isCalendarDate(date) && date >= today && date < "9999-01-01";
}

export function calendarDateLabel(date: string): string {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(new Date(`${date}T00:00:00Z`));
}

export function comparePlannedOccurrences(
  a: OccurrenceView,
  b: OccurrenceView,
) {
  return (
    (a.occurrence.plannedTime ?? "\uffff").localeCompare(
      b.occurrence.plannedTime ?? "\uffff",
    ) ||
    Date.parse(a.occurrence.acceptedAt) - Date.parse(b.occurrence.acceptedAt) ||
    a.occurrence.id.localeCompare(b.occurrence.id)
  );
}

export function calendarPlan(
  occurrences: OccurrenceView[],
  start: string,
  week: boolean,
) {
  const days = Array.from({ length: week ? 7 : 1 }, (_, offset) =>
    shiftCalendarDate(start, offset),
  )
    .filter((date): date is string => date !== null)
    .map((date) => ({ key: date, date, data: [] as OccurrenceView[] }));
  const byDate = new Map(days.map((day) => [day.date, day]));
  const unscheduled: OccurrenceView[] = [];
  for (const item of occurrences) {
    if (!item.occurrence.dueDate) unscheduled.push(item);
    else byDate.get(item.occurrence.dueDate)?.data.push(item);
  }
  for (const day of days) day.data.sort(comparePlannedOccurrences);
  unscheduled.sort(comparePlannedOccurrences);
  return { days, unscheduled };
}
