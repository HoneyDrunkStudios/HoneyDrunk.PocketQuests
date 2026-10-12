import type { OccurrenceView } from "../../shared/contracts";
import { comparePlannedOccurrences } from "./calendar-plan";

export function activePlan(occurrences: OccurrenceView[], today: string) {
  const current: OccurrenceView[] = [];
  const unscheduled: OccurrenceView[] = [];
  const earlier: OccurrenceView[] = [];
  const upcoming: OccurrenceView[] = [];
  const past: OccurrenceView[] = [];
  for (const item of occurrences) {
    if (item.status === "Missed" || item.status === "Abandoned") {
      past.push(item);
      continue;
    }
    // The server owns lifecycle status. A cached or frozen commitment with an
    // earlier date must never be reclassified as missed by the presentation.
    if (item.status !== "Active" && item.status !== "Frozen") continue;
    const date = item.occurrence.dueDate;
    if (!date) unscheduled.push(item);
    else if (date === today) current.push(item);
    else if (date < today) earlier.push(item);
    else upcoming.push(item);
  }
  const byDate = (a: OccurrenceView, b: OccurrenceView) =>
    (a.occurrence.dueDate ?? "").localeCompare(b.occurrence.dueDate ?? "") ||
    comparePlannedOccurrences(a, b);
  current.sort(comparePlannedOccurrences);
  unscheduled.sort(comparePlannedOccurrences);
  earlier.sort(byDate);
  upcoming.sort(byDate);
  return { current, unscheduled, earlier, upcoming, past };
}
