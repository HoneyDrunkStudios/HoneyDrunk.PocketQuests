import type { Quest } from "../../shared/contracts";

export type QuestSearchFilters = {
  query: string;
  categoryId: string | null;
  effort: Quest["effort"] | null;
};

export const defaultQuestSearchFilters: QuestSearchFilters = {
  query: "",
  categoryId: null,
  effort: null,
};

export function searchable(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
}
