import { useState, type ReactNode } from "react";
import { Image, Pressable, Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { focusAppearance } from "@honeydrunk/ui-native";
import { Card, colors, styles } from "../../shared/ui";
import { pocketQuestsTheme } from "../../shared/theme";
import { journalArt } from "../../shared/journal-art";
const paths = {
  Categories: { image: journalArt.compass, color: colors.gold },
  Attributes: { image: journalArt.books, color: "#7DC9B4" },
  Skills: { image: journalArt.sketchbook, color: "#BB91F4" },
};
/** Headings remain outside buttons so native accessibility keeps heading navigation. */
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
  const path = paths[title];
  return (
    <Card
      style={{ padding: 0, borderLeftWidth: 5, borderLeftColor: path.color }}
    >
      <View
        style={{
          minHeight: 58,
          flexDirection: "row",
          alignItems: "center",
          gap: 8,
          paddingVertical: 4,
        }}
      >
        <Image
          source={path.image}
          accessible={false}
          style={{ width: 72, height: 48 }}
          resizeMode="contain"
        />
        <View style={{ flex: 1, gap: 3 }}>
          <Text accessibilityRole="header" style={styles.subtitle}>
            {title}
          </Text>
          <Text style={[styles.muted, { fontSize: 12, lineHeight: 17 }]}>
            {description}
          </Text>
        </View>
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
              minWidth: 58,
              borderRadius: 10,
              flexDirection: "row",
              alignItems: "center",
              justifyContent: "center",
              gap: 4,
              opacity: pressed ? 0.8 : 1,
            },
            focusAppearance(pocketQuestsTheme, focused),
          ]}
        >
          <Text style={[styles.text, { color: path.color }]}>{count}</Text>
          <Ionicons
            name={expanded ? "chevron-down" : "chevron-forward"}
            size={18}
            color={colors.muted}
            accessible={false}
          />
        </Pressable>
      </View>
      {expanded && (
        <View style={{ gap: 4, padding: 16 }}>
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
