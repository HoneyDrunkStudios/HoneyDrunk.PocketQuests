import { Pressable, Text, View } from "react-native";
import { colors, styles } from "../../shared/ui";

export function QuestViewTabs<T extends string>({
  options,
  selected,
  onSelect,
}: Readonly<{
  options: readonly T[];
  selected: T;
  onSelect(value: T): void;
}>) {
  return (
    <View
      accessibilityRole="tablist"
      style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
    >
      {options.map((option) => (
        <Pressable
          key={option}
          accessibilityRole="tab"
          accessibilityState={{ selected: selected === option }}
          aria-selected={selected === option}
          onPress={() => onSelect(option)}
          style={{
            minHeight: 48,
            minWidth: 48,
            padding: 14,
            justifyContent: "center",
            borderRadius: 10,
            borderWidth: 1,
            borderColor: colors.gold,
            backgroundColor: selected === option ? colors.gold : colors.card,
          }}
        >
          <Text
            style={[
              styles.text,
              {
                color: selected === option ? "#FFFFFF" : colors.gold,
                fontWeight: "600",
              },
            ]}
          >
            {option}
          </Text>
        </Pressable>
      ))}
    </View>
  );
}
