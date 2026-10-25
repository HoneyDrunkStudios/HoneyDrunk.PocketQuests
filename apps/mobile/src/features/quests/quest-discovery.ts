import type { Catalog, Quest, State } from "../../shared/contracts";
import { eligible } from "./quest-rules";

export type DiscoveryFilters = {
  query: string;
  categoryId: string | null;
  effort: Quest["effort"] | null;
  source: "all" | "starter" | "custom";
  readyOnly: boolean;
  penaltyFreeOnly: boolean;
  sort: "recommended" | "xp" | "title";
};
export const defaultDiscoveryFilters: DiscoveryFilters = {
  query: "",
  categoryId: null,
  effort: null,
  source: "all",
  readyOnly: false,
  penaltyFreeOnly: false,
  sort: "recommended",
};
export type DiscoveryQuest = {
  quest: Quest;
  categoryName: string;
  ready: boolean;
  searchText: string;
};

function searchable(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
}

/** Index the current available pool once; typing never asks the API for results. */
export function indexDiscoveryQuests(
  quests: Quest[],
  catalog: Catalog | null,
  state: State | null,
): DiscoveryQuest[] {
  const categories = new Map(
    catalog?.categories.map((item) => [item.id, item.name]),
  );
  const skills = new Map(
    [...(catalog?.skills ?? []), ...(state?.profile.customSkills ?? [])].map(
      (item) => [item.id, item.name],
    ),
  );
  const attributes = new Map(
    catalog?.attributes.map((item) => [item.id, item.name]),
  );
  return quests.map((quest) => {
    const categoryName = categories.get(quest.categoryId) ?? quest.categoryId;
    return {
      quest,
      categoryName,
      ready: !!state && eligible(quest, state),
      searchText: searchable(
        [
          quest.title,
          quest.description,
          quest.criterion,
          categoryName,
          quest.effort,
          ...quest.skills.map((item) => skills.get(item.id) ?? ""),
          ...quest.attributes.map((item) => attributes.get(item.id) ?? ""),
        ]
          .filter(Boolean)
          .join(" "),
      ),
    };
  });
}

/** Keep the supplied recommendation order unless the user explicitly changes it. */
export function discoverQuests(
  quests: DiscoveryQuest[],
  filters: DiscoveryFilters,
) {
  const terms = searchable(filters.query).trim().split(/\s+/).filter(Boolean);
  const results = quests.filter(
    ({ quest, ready, searchText }) =>
      (!filters.categoryId || quest.categoryId === filters.categoryId) &&
      (!filters.effort || quest.effort === filters.effort) &&
      (filters.source === "all" ||
        quest.isCustom === (filters.source === "custom")) &&
      (!filters.readyOnly || ready) &&
      (!filters.penaltyFreeOnly || (quest.penaltyPercent ?? 0) === 0) &&
      terms.every((term) => searchText.includes(term)),
  );
  if (filters.sort === "xp")
    results.sort((a, b) => b.quest.baseXp - a.quest.baseXp);
  if (filters.sort === "title")
    results.sort((a, b) => a.quest.title.localeCompare(b.quest.title));
  return results;
}

export function discoveryFilterCount(filters: DiscoveryFilters) {
  return (
    Number(!!filters.categoryId) +
    Number(!!filters.effort) +
    Number(filters.source !== "all") +
    Number(filters.readyOnly) +
    Number(filters.penaltyFreeOnly) +
    Number(filters.sort !== "recommended")
  );
}
