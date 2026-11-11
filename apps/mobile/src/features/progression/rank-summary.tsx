import { useState } from "react";
import { Pressable, Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { focusAppearance } from "@honeydrunk/ui-native";
import { RankInsignia } from "../../shared/rank-insignia";
import { useAppTheme } from "../../shared/ui";
export function RankSummary({
  rank,
  expanded,
  onPress,
}: Readonly<{ rank: string; expanded: boolean; onPress(): void }>) {
  const { styles, colors, theme } = useAppTheme();
  const [focused, setFocused] = useState(false);
  return (
    <View style={{ flexDirection: "row", alignItems: "center", gap: 14 }}>
      <View
        style={{
          width: 64,
          height: 80,
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <RankInsignia rank={rank} size={56} label={false} />
      </View>
      <View style={{ flex: 1, gap: 4 }}>
        <Text accessibilityRole="header" style={styles.subtitle}>
          Global rank {rank}
        </Text>
        <Text style={[styles.muted, { fontSize: 13, lineHeight: 18 }]}>
          Your growth across all ten categories.
        </Text>
        <View
          pointerEvents="none"
          style={{
            flexDirection: "row",
            alignItems: "center",
            justifyContent: "flex-end",
            gap: 8,
          }}
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
        </View>
      </View>
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
            position: "absolute",
            inset: 0,
            minHeight: 48,
            borderRadius: 6,
            backgroundColor: pressed ? "#FFFFFF0A" : "transparent",
          },
          focusAppearance(theme, focused),
        ]}
      />
    </View>
  );
}
