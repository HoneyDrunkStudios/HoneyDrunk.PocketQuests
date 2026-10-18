import { useState } from "react";
import { Text, View } from "react-native";
import type { Catalog, State } from "../../shared/contracts";
import { Button, Label, Progress, styles } from "../../shared/ui";

/** Display API requirements and balances; promotion remains server-owned. */
export function RankRequirements({
  rank,
  categories,
  rules,
}: Readonly<{
  rank: State["rank"];
  categories: State["categories"];
  rules: Catalog["rules"];
}>) {
  const [expanded, setExpanded] = useState(false);
  const [showCategories, setShowCategories] = useState(false);
  const target = rank.requirement;
  const highest = rank.current === "S";
  return (
    <View style={{ gap: 16 }}>
      <Label>Overall level is separate from rank.</Label>
      {highest ? (
        <Label>All ten paths contribute to rank S.</Label>
      ) : (
        <>
          <Text accessibilityRole="header" style={styles.subtitle}>
            Toward rank {target.rank}
          </Text>
          <Label>Meet both requirements to reach the next rank.</Label>
          <Progress
            label="Qualifying categories"
            value={rank.qualifyingCategories}
            max={target.count}
            valueText={`${rank.qualifyingCategories}/${target.count} categories at ${target.floor} XP each`}
          />
          <Progress
            label="Combined category XP"
            value={rank.total}
            max={target.total}
            valueText={`${rank.total}/${target.total} XP`}
          />
          <Button
            secondary
            title={
              showCategories
                ? "Hide category requirements"
                : "See category requirements"
            }
            onPress={() => setShowCategories((shown) => !shown)}
          />
          {showCategories &&
            categories.map((category) => {
              const remaining = Math.max(0, target.floor - category.xp);
              return (
                <View key={category.id} style={{ gap: 4 }}>
                  <Label>{category.name}</Label>
                  <Label>
                    {category.xp} XP ·{" "}
                    {remaining === 0
                      ? "Floor met"
                      : `${remaining} XP to the floor`}
                  </Label>
                </View>
              );
            })}
        </>
      )}
      {rules.length > 0 ? (
        <>
          <Button
            secondary
            title={expanded ? "Hide rank ladder" : "See the rank ladder"}
            onPress={() => setExpanded((shown) => !shown)}
          />
          {expanded &&
            rules.map((rule) => (
              <View key={rule.rank} style={{ gap: 4, paddingVertical: 8 }}>
                <Text accessibilityRole="header" style={styles.subtitle}>
                  Rank {rule.rank}
                  {rule.rank === rank.current ? " · Current rank" : ""}
                </Text>
                <Label>
                  {rule.rank === "F"
                    ? "Your starting rank"
                    : `${rule.count} categories at ${rule.floor} XP each, and ${rule.total} combined category XP`}
                </Label>
              </View>
            ))}
        </>
      ) : (
        <Label>
          The rank ladder will appear when the catalog is available.
        </Label>
      )}
    </View>
  );
}
