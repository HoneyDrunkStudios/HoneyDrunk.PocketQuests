import type { State } from "../../shared/contracts";

export type RecapPeriod = 7 | 30;
type RecapState = Pick<
  State,
  "today" | "zone" | "occurrences" | "categories" | "ledger"
>;
export type RecapEntry = {
  id: string;
  occurrenceId: string;
  title: string;
  categoryId: string;
  categoryName: string;
  day: string;
  recordedAt: string;
  xp: number;
};

export function recapDateLabel(day: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(new Date(`${day}T00:00:00Z`));
}

/** Read-only summary of surviving, confirmed completions. Never calculates rewards. */
export function activityRecap(state: RecapState, period: RecapPeriod) {
  const end = Date.parse(`${state.today}T00:00:00Z`);
  if (
    !Number.isFinite(end) ||
    new Date(end).toISOString().slice(0, 10) !== state.today
  )
    return null;
  let formatter: Intl.DateTimeFormat;
  try {
    formatter = new Intl.DateTimeFormat("en-US", {
      timeZone: state.zone,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    });
  } catch {
    return null;
  }
  // UTC is only a calendar-date container. Completion instants use the account zone.
  const days = Array.from({ length: period }, (_, index) => ({
    day: new Date(end - (period - 1 - index) * 86_400_000)
      .toISOString()
      .slice(0, 10),
    count: 0,
  }));
  const byDay = new Map(days.map((day) => [day.day, day]));
  const categories = new Map(
    state.categories.map((category) => [category.id, category.name]),
  );
  const awards = new Map<string, number>();
  for (const entry of state.ledger ?? []) {
    if (entry.track === "Overall" && entry.trackId === "overall")
      awards.set(`${entry.eventId}:${entry.occurrenceId}`, entry.amount);
  }
  const entries: RecapEntry[] = [];
  const seen = new Set<string>();
  for (const item of state.occurrences) {
    const completion = item.completion;
    if (
      item.status !== "Completed" ||
      !completion ||
      item.pendingCompletion ||
      item.pendingTimingVerification ||
      seen.has(completion.id)
    )
      continue;
    const xp = awards.get(`${completion.id}:${item.occurrence.id}`);
    if (xp === undefined) continue;
    const day = completionDay(formatter, completion.recordedAt);
    const bucket = byDay.get(day);
    if (!bucket) continue;
    seen.add(completion.id);
    bucket.count++;
    const quest = completion.snapshot ?? item.occurrence.quest;
    entries.push({
      id: completion.id,
      occurrenceId: item.occurrence.id,
      title: quest.title,
      categoryId: quest.categoryId,
      categoryName: categories.get(quest.categoryId) ?? "Other category",
      day,
      recordedAt: completion.recordedAt,
      xp,
    });
  }
  entries.sort(
    (a, b) =>
      Date.parse(b.recordedAt) - Date.parse(a.recordedAt) ||
      a.id.localeCompare(b.id),
  );
  const paths = new Map<string, { id: string; name: string; count: number }>();
  for (const entry of entries) {
    const path = paths.get(entry.categoryId) ?? {
      id: entry.categoryId,
      name: entry.categoryName,
      count: 0,
    };
    path.count++;
    paths.set(path.id, path);
  }
  return {
    start: days[0].day,
    end: state.today,
    days,
    entries,
    completed: entries.length,
    activeDays: days.filter((day) => day.count > 0).length,
    xp: entries.reduce((total, entry) => total + entry.xp, 0),
    paths: [...paths.values()].sort(
      (a, b) => b.count - a.count || a.name.localeCompare(b.name),
    ),
  };
}

function completionDay(formatter: Intl.DateTimeFormat, recordedAt: string) {
  const instant = new Date(recordedAt);
  if (!Number.isFinite(instant.getTime())) return "";
  const parts = formatter.formatToParts(instant);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((value) => value.type === type)?.value;
  return `${part("year")}-${part("month")}-${part("day")}`;
}
