import { QuestFilterToggle } from "./quest-filter-toggle";
import { useState } from "react";
import { Text, View } from "react-native";
import type { Catalog } from "../../shared/contracts";
import { Button, Input, Label, styles } from "../../shared/ui";
import { QuestCategoryEffortFilters } from "./quest-category-effort-filters";
import type { QuestSearchFilters } from "./quest-search";

export function QuestHistoryControls({
  view,
  categories,
  filters,
  resultCount,
  onChange,
  onReset,
}: Readonly<{
  view: "Active" | "Completed";
  categories: Catalog["categories"];
  filters: QuestSearchFilters;
  resultCount: number;
  onChange(change: Partial<QuestSearchFilters>): void;
  onReset(): void;
}>) {
  const [expanded, setExpanded] = useState(false);
  const count = Number(!!filters.categoryId) + Number(!!filters.effort);
  const summary = [
    categories.find((category) => category.id === filters.categoryId)?.name,
    filters.effort && `${filters.effort} effort`,
  ]
    .filter(Boolean)
    .join(" · ");
  return (
    <View style={{ gap: 12 }}>
      <Input
        accessibilityLabel={`Search ${view.toLowerCase()} quests`}
        placeholder="Title, category, or completion goal"
        value={filters.query}
        onChangeText={(query) => onChange({ query })}
        autoCapitalize="none"
        autoCorrect={false}
        returnKeyType="search"
        maxLength={200}
        style={styles.input}
      />
      <Text accessibilityLiveRegion="polite" style={styles.muted}>
        {resultCount} {resultCount === 1 ? "quest" : "quests"} found.
      </Text>
      <QuestFilterToggle
        expanded={expanded}
        count={count}
        onPress={() => setExpanded((value) => !value)}
      />
      {!!summary && <Label>{summary}</Label>}
      {expanded && (
        <QuestCategoryEffortFilters
          categories={categories}
          filters={filters}
          onChange={onChange}
        />
      )}
      {(!!filters.query || count > 0) && (
        <Button title="Clear search and filters" secondary onPress={onReset} />
      )}
    </View>
  );
}
