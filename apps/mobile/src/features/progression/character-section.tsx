import { useState, type ReactNode } from "react";
import { Pressable, Text, View } from "react-native";
import { focusAppearance } from "@honeydrunk/ui-native";
import { Card, colors, styles } from "../../shared/ui";
import { pocketQuestsTheme } from "../../shared/theme";

/** A character-sheet track group; expansion never owns or copies balances. */
export function CharacterSection({
  title,
  description,
  count,
  children,
}: Readonly<{
  title: "Categories" | "Attributes" | "Skills";
  description: string;
  count: number;
  children: ReactNode;
}>) {
  const [expanded, setExpanded] = useState(false);
  const [focused, setFocused] = useState(false);
  return (
    <Card style={{ gap: 16 }}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`${title}, ${count}`}
        accessibilityHint={description}
        accessibilityState={{ expanded }}
        aria-expanded={expanded}
        onPress={() => setExpanded((value) => !value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        style={({ pressed }) => [
          {
            minHeight: 48,
            borderRadius: 10,
            padding: 8,
            gap: 8,
            opacity: pressed ? 0.8 : 1,
          },
          focusAppearance(pocketQuestsTheme, focused),
        ]}
      >
        <View
          style={{
            flexDirection: "row",
            flexWrap: "wrap",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 8,
          }}
        >
          <Text style={styles.subtitle}>{title}</Text>
          <Text style={[styles.text, { color: colors.gold }]}>{count}</Text>
        </View>
        <Text style={styles.muted}>{description}</Text>
        <Text style={[styles.text, { color: colors.gold }]}>
          {expanded ? "Hide details" : "Show details"}
        </Text>
      </Pressable>
      {expanded && (
        <View style={{ gap: 4 }}>
          {count === 0 ? (
            <Text style={styles.text}>
              No {title.toLowerCase()} to show yet.
            </Text>
          ) : (
            children
          )}
        </View>
      )}
    </Card>
  );
}
