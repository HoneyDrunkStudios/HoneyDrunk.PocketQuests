import { QuestFilterToggle } from "./quest-filter-toggle";
import { QuestFilterChoice as Choice } from "./quest-filter-choice";
import { QuestCategoryEffortFilters } from "./quest-category-effort-filters";
import { useState } from "react";
import { Text, View } from "react-native";
import type { Catalog } from "../../shared/contracts";
import { Button, Input, Label, styles } from "../../shared/ui";
import { discoveryFilterCount, type DiscoveryFilters } from "./quest-discovery";

const sourceLabels = {
  all: null,
  starter: "Starter quests",
  custom: "Custom quests",
} as const;
const sortLabels = {
  recommended: null,
  xp: "Most XP first",
  title: "Title A–Z",
} as const;

export function QuestDiscoveryControls({
  categories,
  filters,
  resultCount,
  shownCount,
  onChange,
  onReset,
}: Readonly<{
  categories: Catalog["categories"];
  filters: DiscoveryFilters;
  resultCount: number;
  shownCount: number;
  onChange(change: Partial<DiscoveryFilters>): void;
  onReset(): void;
}>) {
  const [expanded, setExpanded] = useState(false);
  const count = discoveryFilterCount(filters);
  const summary = [
    categories.find((item) => item.id === filters.categoryId)?.name,
    filters.effort && `${filters.effort} effort`,
    sourceLabels[filters.source],
    filters.readyOnly && "Ready to start",
    filters.penaltyFreeOnly && "No XP penalties",
    sortLabels[filters.sort],
  ]
    .filter(Boolean)
    .join(" · ");
  return (
    <View style={{ gap: 12 }}>
      <Label>Find a quest that fits</Label>
      <Input
        accessibilityLabel="Search quests"
        placeholder="Title, topic, or skill"
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
        {resultCount > shownCount ? ` Showing the first ${shownCount}.` : ""}
      </Text>
      <QuestFilterToggle
        expanded={expanded}
        count={count}
        onPress={() => setExpanded((value) => !value)}
      />
      {!!summary && <Label>{summary}</Label>}
      {expanded && (
        <View style={{ gap: 12 }}>
          <QuestCategoryEffortFilters
            categories={categories}
            filters={filters}
            onChange={onChange}
          />
          <Label>Quest type</Label>
          <View
            accessibilityRole="radiogroup"
            accessibilityLabel="Quest type"
            style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
          >
            <Choice
              title="All quests"
              selected={filters.source === "all"}
              onPress={() => onChange({ source: "all" })}
            />
            <Choice
              title="Starter quests"
              selected={filters.source === "starter"}
              onPress={() => onChange({ source: "starter" })}
            />
            <Choice
              title="Custom quests"
              selected={filters.source === "custom"}
              onPress={() => onChange({ source: "custom" })}
            />
          </View>
          <Choice
            checkbox
            title="Ready to start"
            selected={filters.readyOnly}
            onPress={() => onChange({ readyOnly: !filters.readyOnly })}
          />
          <Choice
            checkbox
            title="No XP penalties"
            selected={filters.penaltyFreeOnly}
            onPress={() =>
              onChange({ penaltyFreeOnly: !filters.penaltyFreeOnly })
            }
          />
          <Label>Sort by</Label>
          <View
            accessibilityRole="radiogroup"
            accessibilityLabel="Quest sorting"
            style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
          >
            <Choice
              title="Recommended"
              selected={filters.sort === "recommended"}
              onPress={() => onChange({ sort: "recommended" })}
            />
            <Choice
              title="Most XP"
              selected={filters.sort === "xp"}
              onPress={() => onChange({ sort: "xp" })}
            />
            <Choice
              title="Title A–Z"
              selected={filters.sort === "title"}
              onPress={() => onChange({ sort: "title" })}
            />
          </View>
          <Label>
            Recommended puts your interests first. Locked quests can still be
            opened to see what they need.
          </Label>
        </View>
      )}
      {(!!filters.query || count > 0) && (
        <Button title="Clear search and filters" secondary onPress={onReset} />
      )}
    </View>
  );
}
