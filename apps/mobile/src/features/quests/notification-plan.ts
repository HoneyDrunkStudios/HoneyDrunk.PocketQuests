import type { State } from "../../shared/contracts";
const warningLeadTimeMs = 60 * 60 * 1_000;
const maximumPendingWarnings = 60;

// One generic warning per due-day cutoff; at most 60 pending dates leaves room
// under iOS's notification limit. Opening/reconnecting refreshes the forecast.
export function warningTimes(state: State | null, now: number): number[] {
  if (!state?.profile.expiryWarnings || state.schedule.accountPaused) return [];
  const deadlines = [
    ...state.occurrences
      .filter(
        (o) =>
          o.status === "Active" &&
          !state.schedule.pausedCategories.includes(
            o.occurrence.quest.categoryId,
          ),
      )
      .map((o) => o.occurrence.deadline),
    ...(state.futureWarnings ?? []),
  ];
  return [
    ...new Set(
      deadlines
        .filter((d): d is string => !!d)
        .map((d) => Date.parse(d) - warningLeadTimeMs),
    ),
  ]
    .filter((at) => Number.isFinite(at) && at > now)
    .sort((a, b) => a - b)
    .slice(0, maximumPendingWarnings);
}
