import { Text, View } from "react-native";
import { useAccountSnapshot } from "../../session/session";
import { Button, Label, useAppTheme } from "../../shared/ui";
import { useAppearanceAutosave } from "./appearance-autosave";

function RewardChoice({ kind }: Readonly<{ kind: "Badge" | "Frame" }>) {
  const { styles } = useAppTheme();
  const { state } = useAccountSnapshot();
  const appearance = useAppearanceAutosave();
  if (!state) return null;
  const choice = kind === "Badge" ? appearance.badgeId : appearance.frameId;
  const fallback = kind === "Badge" ? "No badge" : "Default frame";
  return (
    <View style={{ gap: 10 }}>
      <Text style={styles.subtitle}>{kind}</Text>
      <Button
        title={fallback}
        secondary={choice !== null}
        accessibilityLabel={`${choice === null ? "Selected: " : ""}${fallback}`}
        onPress={() => appearance.changeReward(kind, null)}
      />
      {state.entitlements
        .filter((item) => item.earned && item.kind === kind)
        .map((item) => (
          <Button
            key={item.id}
            title={`${choice === item.id ? "Selected" : "Choose"} ${kind.toLowerCase()}: ${item.name}`}
            secondary={choice !== item.id}
            onPress={() => appearance.changeReward(kind, item.id)}
          />
        ))}
    </View>
  );
}
export function AppearanceEditor() {
  const { styles } = useAppTheme();
  return (
    <View style={{ gap: 16 }}>
      <Text style={styles.subtitle}>Your look</Text>
      <Label>Choose a badge or frame. Changes save automatically.</Label>
      <RewardChoice kind="Badge" />
      <RewardChoice kind="Frame" />
    </View>
  );
}
