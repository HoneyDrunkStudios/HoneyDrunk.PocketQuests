import { ScrollView } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { colors } from "../shared/ui";
import { SessionRecovery } from "./session-recovery";
export function Page({ children }: { children: React.ReactNode }) {
  const insets = useSafeAreaInsets();
  return (
    <ScrollView
      contentInsetAdjustmentBehavior="automatic"
      style={{ flex: 1, backgroundColor: colors.paper }}
      contentContainerStyle={{
        padding: 20,
        paddingBottom: Math.max(insets.bottom, 20) + 24,
        gap: 20,
        width: "100%",
        maxWidth: 780,
        alignSelf: "center",
      }}
    >
      <SessionRecovery />
      {children}
    </ScrollView>
  );
}
