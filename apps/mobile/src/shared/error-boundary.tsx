import { useEffect } from "react";
import type { ErrorBoundaryProps } from "expo-router";
import { Pressable, ScrollView, StyleSheet, Text } from "react-native";
import { reportRenderFailure } from "./crash-reporting";

export function AppErrorBoundary({ retry }: ErrorBoundaryProps) {
  useEffect(() => {
    reportRenderFailure();
  }, []);
  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text accessibilityRole="header" style={styles.title}>
        Pocket Quests could not display this screen
      </Text>
      <Text accessibilityRole="alert" style={styles.copy}>
        Your saved actions are still on this device. Try reopening the screen.
        If it continues, close and reopen the app.
      </Text>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Try opening the screen again"
        style={styles.button}
        onPress={() => {
          void retry();
        }}
      >
        <Text style={styles.label}>Try again</Text>
      </Pressable>
    </ScrollView>
  );
}
const styles = StyleSheet.create({
  page: {
    flexGrow: 1,
    justifyContent: "center",
    padding: 24,
    gap: 20,
    backgroundColor: "#171719",
  },
  title: { color: "#f5f0e6", fontSize: 24, fontWeight: "700" },
  copy: { color: "#f5f0e6", fontSize: 18 },
  button: {
    minWidth: 48,
    minHeight: 48,
    padding: 16,
    backgroundColor: "#f5f0e6",
    borderRadius: 8,
  },
  label: { color: "#171719", fontSize: 18, textAlign: "center" },
});
