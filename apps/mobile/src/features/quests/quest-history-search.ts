import type { Catalog, OccurrenceView } from "../../shared/contracts";
import { searchable, type QuestSearchFilters } from "./quest-search";

/** Search the accepted snapshot, including quests whose definition was archived. */
export function indexQuestHistory(
  occurrences: OccurrenceView[],
  categories: Catalog["categories"],
) {
  const names = new Map(
    categories.map((category) => [category.id, category.name]),
  );
  return new Map(
    occurrences.map((item) => {
      const quest = item.occurrence.quest;
      return [
        item.occurrence.id,
        searchable(
          [
            quest.title,
            quest.description,
            quest.criterion,
            names.get(quest.categoryId) ?? quest.categoryId,
            quest.effort,
          ]
            .filter(Boolean)
            .join(" "),
        ),
      ];
    }),
  );
}

/** A predicate keeps each group's existing order and never mutates its snapshots. */
export function questHistoryMatcher(
  index: Map<string, string>,
  filters: QuestSearchFilters,
) {
  const terms = searchable(filters.query).trim().split(/\s+/).filter(Boolean);
  return (item: OccurrenceView) => {
    const quest = item.occurrence.quest;
    return (
      (!filters.categoryId || quest.categoryId === filters.categoryId) &&
      (!filters.effort || quest.effort === filters.effort) &&
      terms.every((term) =>
        (index.get(item.occurrence.id) ?? "").includes(term),
      )
    );
  };
}
