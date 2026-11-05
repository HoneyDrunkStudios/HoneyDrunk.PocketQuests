import type { Catalog, State } from "../../shared/contracts";
import { activePlan } from "./active-plan";
import { availableQuests } from "./quest-availability";
import { eligible } from "./quest-rules";

/** Read-only shortlist; lifecycle, eligibility and awards remain server-owned. */
export function dailyPlan(state: State) {
  const plan = activePlan(state.occurrences, state.today);
  const today = plan.current.filter((item) => item.status === "Active");
  const unscheduled = plan.unscheduled.filter(
    (item) => item.status === "Active",
  );
  return {
    today,
    unscheduled,
    next: [...today, ...unscheduled].slice(0, 3),
    earlier: plan.earlier.filter((item) => item.status === "Active").length,
    paused: state.occurrences.filter((item) => item.status === "Frozen").length,
    upcoming: plan.upcoming.filter((item) => item.status === "Active").length,
  };
}

/** Small, penalty-free ideas from the existing available pool; never accepts one. */
export function dailyIdeas(catalog: Catalog | null, state: State) {
  const categories = new Set<string>();
  const pendingDefinitions = new Set(
    state.definitions
      .filter((item) => item.pendingSave)
      .map((item) => item.quest.id),
  );
  return availableQuests(catalog, state)
    .filter((quest) => {
      if (
        !eligible(quest, state) ||
        quest.effort !== "Small" ||
        (quest.penaltyPercent ?? 0) !== 0 ||
        pendingDefinitions.has(quest.id) ||
        categories.has(quest.categoryId)
      )
        return false;
      categories.add(quest.categoryId);
      return true;
    })
    .slice(0, 3);
}
