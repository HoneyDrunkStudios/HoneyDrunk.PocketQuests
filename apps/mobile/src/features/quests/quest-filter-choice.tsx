import { Pressable, Text } from "react-native";
import { colors, styles } from "../../shared/ui";

export function QuestFilterChoice({
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
