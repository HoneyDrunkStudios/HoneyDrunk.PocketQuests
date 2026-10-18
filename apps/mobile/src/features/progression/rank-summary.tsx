import { useState } from "react";
import { ImageBackground, Pressable, Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { focusAppearance } from "@honeydrunk/ui-native";
import { journalArt } from "../../shared/journal-art";
import { pocketQuestsTheme } from "../../shared/theme";
import { colors, styles } from "../../shared/ui";
export function RankSummary({
  rank,
  expanded,
  onPress,
}: Readonly<{ rank: string; expanded: boolean; onPress(): void }>) {
  const [focused, setFocused] = useState(false);
  return (
    <View style={{ flexDirection: "row", alignItems: "center", gap: 14 }}>
      <ImageBackground
        source={journalArt.shield}
        resizeMode="contain"
        style={{
          width: 64,
          height: 80,
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <Text accessible={false} style={[styles.title, { fontSize: 32 }]}>
          {rank}
        </Text>
      </ImageBackground>
      <View style={{ flex: 1, gap: 4 }}>
        <Text accessibilityRole="header" style={styles.subtitle}>
          Global rank {rank}
        </Text>
        <Text style={[styles.muted, { fontSize: 13, lineHeight: 18 }]}>
          Your growth across all ten categories.
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={
            expanded ? "Hide rank progress" : "View rank progress"
          }
          accessibilityState={{ expanded }}
          aria-expanded={expanded}
          onPress={onPress}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          style={({ pressed }) => [
            {
              minHeight: 48,
              flexDirection: "row",
              alignItems: "center",
              justifyContent: "flex-end",
              gap: 8,
              opacity: pressed ? 0.8 : 1,
              borderRadius: 6,
            },
            focusAppearance(pocketQuestsTheme, focused),
          ]}
        >
          <Text style={[styles.muted, { color: colors.gold, flexShrink: 1 }]}>
            {expanded ? "Hide rank progress" : "View rank progress"}
          </Text>
          <Ionicons
            name={expanded ? "chevron-up" : "arrow-forward"}
            size={18}
            color={colors.gold}
            accessible={false}
          />
        </Pressable>
      </View>
    </View>
  );
}
