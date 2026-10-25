import { useState } from "react";
import { Pressable, Text, View } from "react-native";
import type { Catalog } from "../../shared/contracts";
import { Button, Input, Label, colors, styles } from "../../shared/ui";
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

function Choice({
  title,
  selected,
  checkbox = false,
  onPress,
}: Readonly<{
  title: string;
  selected: boolean;
  checkbox?: boolean;
  onPress(): void;
}>) {
  return (
    <Pressable
      accessibilityRole={checkbox ? "checkbox" : "radio"}
      accessibilityLabel={title}
      accessibilityState={{ checked: selected }}
      aria-checked={selected}
      onPress={onPress}
      style={{
        minHeight: 48,
        flexShrink: 1,
        padding: 12,
        borderWidth: 1,
        borderColor: colors.gold,
        borderRadius: 10,
        backgroundColor: selected ? colors.gold : colors.card,
      }}
    >
      <Text
        style={[styles.text, { color: selected ? colors.onGold : colors.gold }]}
      >
        {title}
      </Text>
    </Pressable>
  );
}

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
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ expanded }}
        accessibilityLabel={
          expanded ? "Hide quest filters" : "Show quest filters"
        }
        aria-expanded={expanded}
        onPress={() => setExpanded((value) => !value)}
        style={{
          minHeight: 48,
          padding: 12,
          borderWidth: 1,
          borderColor: colors.line,
          borderRadius: 10,
        }}
      >
        <Text style={styles.text}>
          {expanded ? "Hide filters" : "Show filters"}
          {count ? ` (${count})` : ""}
        </Text>
      </Pressable>
      {!!summary && <Label>{summary}</Label>}
      {expanded && (
        <View style={{ gap: 12 }}>
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
