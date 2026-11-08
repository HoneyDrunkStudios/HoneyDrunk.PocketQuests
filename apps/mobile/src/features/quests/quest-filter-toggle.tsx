import { Pressable, Text } from "react-native";
import { useAppTheme } from "../../shared/ui";

export function QuestFilterToggle({
  expanded,
  count,
  onPress,
}: Readonly<{
  expanded: boolean;
  count: number;
  onPress(): void;
}>) {
  const { colors, styles } = useAppTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ expanded }}
      accessibilityLabel={
        expanded ? "Hide quest filters" : "Show quest filters"
      }
      aria-expanded={expanded}
      onPress={onPress}
      style={{
        minHeight: 48,
        padding: 12,
        borderWidth: 1,
        borderColor: colors.line,
        borderRadius: 10,
        alignSelf: "flex-start",
      }}
    >
      <Text style={styles.text}>
        {expanded ? "Hide filters" : "Show filters"}
        {count ? ` (${count})` : ""}
      </Text>
    </Pressable>
  );
}
