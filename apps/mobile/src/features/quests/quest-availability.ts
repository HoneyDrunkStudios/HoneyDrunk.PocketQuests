import type { Catalog, State } from "../../shared/contracts";

/** Available definitions use current account data, never a copied detail snapshot. */
export function availableQuests(catalog: Catalog | null, state: State | null) {
  if (!state || !catalog) return [];
  const commitments = new Set(
    state.occurrences
      .filter((item) => ["Active", "Frozen"].includes(item.status))
      .map((item) => item.occurrence.quest.id),
  );
  return [
    ...catalog.quests,
    ...state.definitions
      .filter((item) => !item.archived)
      .map((item) => item.quest),
  ]
    .filter((quest) => !commitments.has(quest.id))
    .sort(
      (a, b) =>
        Number(state.profile.interests.includes(b.categoryId)) -
        Number(state.profile.interests.includes(a.categoryId)),
    );
}
