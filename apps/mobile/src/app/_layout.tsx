import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { SessionProvider } from "../session/session";
import { ThemeProvider } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "../shared/theme";
import { colors } from "../shared/ui";
import { CompletionOverlay } from "../features/progression/completion-overlay";
export { AppErrorBoundary as ErrorBoundary } from "../shared/error-boundary";
export default function Layout() {
  return (
    <ThemeProvider theme={pocketQuestsTheme}>
      <StatusBar style="light" />
      <SessionProvider>
        <Stack
          screenOptions={{
            headerStyle: { backgroundColor: colors.paper },
            headerTintColor: colors.ink,
          }}
        >
          <Stack.Screen name="index" options={{ title: "Pocket Quests" }} />
          <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
          <Stack.Screen name="quest/[id]" options={{ title: "Quest" }} />
        </Stack>
        <CompletionOverlay />
      </SessionProvider>
    </ThemeProvider>
  );
}
