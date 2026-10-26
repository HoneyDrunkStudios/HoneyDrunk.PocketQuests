import { View } from "react-native";
import type { Catalog } from "../../shared/contracts";
import { Label } from "../../shared/ui";
import { QuestFilterChoice as Choice } from "./quest-filter-choice";
import type { QuestSearchFilters } from "./quest-search";

export function QuestCategoryEffortFilters({
  categories,
  filters,
  onChange,
}: Readonly<{
  categories: Catalog["categories"];
  filters: QuestSearchFilters;
  onChange(change: Partial<QuestSearchFilters>): void;
}>) {
  return (
    <>
      <Label>Category</Label>
      <View
        accessibilityRole="radiogroup"
        accessibilityLabel="Quest category"
        style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
      >
        <Choice
          title="All categories"
          selected={!filters.categoryId}
          onPress={() => onChange({ categoryId: null })}
        />
        {categories.map((item) => (
          <Choice
            key={item.id}
            title={item.name}
            selected={filters.categoryId === item.id}
            onPress={() => onChange({ categoryId: item.id })}
          />
        ))}
      </View>
      <Label>Effort</Label>
      <View
        accessibilityRole="radiogroup"
        accessibilityLabel="Quest effort"
        style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
      >
        <Choice
          title="Any effort"
          selected={!filters.effort}
          onPress={() => onChange({ effort: null })}
        />
        {(["Small", "Medium", "Large"] as const).map((effort) => (
          <Choice
            key={effort}
            title={effort}
            selected={filters.effort === effort}
            onPress={() => onChange({ effort })}
          />
        ))}
      </View>
    </>
  );
}
