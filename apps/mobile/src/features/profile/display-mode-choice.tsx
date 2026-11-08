import { Pressable, Text, View } from "react-native";
import { useDisplayMode } from "../../session/display-mode";
import { Label, useAppTheme } from "../../shared/ui";
import type { DisplayMode } from "../../shared/theme";
export function DisplayModeChoice() {
  const { colors, styles } = useAppTheme();
  const { mode, ready, error, selectMode } = useDisplayMode();
  return (
    <View style={{ gap: 12 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Journal theme
      </Text>
      <Label>Choose your light. System follows your device.</Label>
      <View
        accessibilityRole="radiogroup"
        accessibilityLabel="Journal theme"
        style={{ flexDirection: "row", gap: 8 }}
      >
        {(["light", "dark", "system"] as DisplayMode[]).map((choice) => (
          <Pressable
            key={choice}
            accessibilityRole="radio"
            aria-checked={mode === choice}
            accessibilityLabel={choice[0].toUpperCase() + choice.slice(1)}
            accessibilityState={{ checked: mode === choice, disabled: !ready }}
            disabled={!ready}
            onPress={() => selectMode(choice)}
            style={{
              flex: 1,
              minHeight: 48,
              borderRadius: 12,
              borderWidth: 1,
              borderColor: colors.green,
              backgroundColor: mode === choice ? colors.green : colors.paper,
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            <Text
              style={[
                styles.text,
                { color: mode === choice ? colors.onGold : colors.green },
              ]}
            >
              {choice[0].toUpperCase() + choice.slice(1)}
            </Text>
          </Pressable>
        ))}
      </View>
      {!!error && <Label accessibilityRole="alert">{error}</Label>}
    </View>
  );
}
