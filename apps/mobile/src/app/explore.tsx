import { View } from "react-native";
import { ExplorationScreen } from "../features/progression/exploration-screen";
import { SessionRecovery } from "../session/session-recovery";
import { useAppTheme } from "../shared/ui";

export default function Explore() {
  const { colors } = useAppTheme();
  return (
    <View style={{ flex: 1, backgroundColor: colors.paper }}>
      <SessionRecovery />
      <ExplorationScreen />
    </View>
  );
}
