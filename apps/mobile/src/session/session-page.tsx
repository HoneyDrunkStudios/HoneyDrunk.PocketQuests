import { ScrollView } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { colors } from "../shared/ui";
import { SessionRecovery } from "./session-recovery";
export function Page({
  children,
  header,
}: {
  children: React.ReactNode;
  header?: React.ReactNode;
}) {
  const insets = useSafeAreaInsets();
  return (
    <ScrollView
      keyboardShouldPersistTaps="handled"
      keyboardDismissMode="on-drag"
      showsVerticalScrollIndicator={false}
      contentInsetAdjustmentBehavior={header ? "never" : "automatic"}
      style={{ flex: 1, backgroundColor: colors.paper }}
      contentContainerStyle={{
        padding: 16,
        paddingTop: header ? 0 : 20,
        paddingBottom: Math.max(insets.bottom, 20) + 24,
        gap: header ? 6 : 10,
        width: "100%",
        maxWidth: 780,
        alignSelf: "center",
      }}
    >
      {header}
      <SessionRecovery />
      {children}
    </ScrollView>
  );
}
